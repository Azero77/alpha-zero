using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Xunit;

namespace Identity.Tests.Integration.Abstractions;

[Collection("IdentityIntegration")]
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
            SchemasToInclude = ["public"] // Identity doesn't use a custom schema yet
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
        var provider = _scope.ServiceProvider.GetRequiredService<ITenantProvider>() as TestTenantProvider;
        if (provider != null)
        {
            provider.CurrentTenantId = tenantId;
        }
    }
}

[CollectionDefinition("IdentityIntegration")]
public class IntegrationCollection : ICollectionFixture<ApiFactory> { }
