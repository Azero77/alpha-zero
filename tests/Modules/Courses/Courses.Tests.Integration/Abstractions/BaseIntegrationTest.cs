using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Xunit;

namespace Courses.Tests.Integration.Abstractions;

[Collection("Integration")]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private readonly IServiceScope _scope;
    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;
    protected readonly AppDbContext DbContext;
    private Respawner _respawner = null!;

    protected BaseIntegrationTest(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        DbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public async Task InitializeAsync()
    {
        // We'll initialize Respawner once or here? 
        // Respawn needs a DB connection. Since we use Testcontainers, the container is shared.
        // The DbContext already points to it.
        
        var connection = DbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = [AppDbContext.Schema]
        });

        await _respawner.ResetAsync(connection);
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    protected void SetTenant(Guid tenantId)
    {
        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Resolve from the test's private scope so the local DbContext sees it
        var provider = _scope.ServiceProvider.GetRequiredService<ITenantProvider>() as TestTenantProvider;
        if (provider != null)
        {
            provider.CurrentTenantId = tenantId;
        }
    }

    protected async Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
    {
        // Use the existing test scope so it shares the same TenantProvider state
        await action(DbContext);
    }

    protected async Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        // Use the existing test scope so it shares the same TenantProvider state
        return await action(DbContext);
    }
}
