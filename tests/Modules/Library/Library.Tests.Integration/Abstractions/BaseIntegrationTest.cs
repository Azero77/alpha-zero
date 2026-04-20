using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Library.Tests.Integration.Abstractions;

namespace Library.Tests.Integration.Abstractions;

[Collection("LibraryIntegration")]
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

    protected T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public async Task InitializeAsync()
    {
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

        var provider = _scope.ServiceProvider.GetRequiredService<ITenantProvider>() as TestTenantProvider;
        if (provider != null)
        {
            provider.CurrentTenantId = tenantId;
        }
    }
}

[CollectionDefinition("LibraryIntegration")]
public class IntegrationCollection : ICollectionFixture<ApiFactory> { }