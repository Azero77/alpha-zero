using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Xunit;

namespace Courses.Tests.Integration.Abstractions;

[Collection("Integration")]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private static Respawner? _respawner;
    private static readonly SemaphoreSlim _respawnerLock = new(1, 1);

    private readonly IServiceScope _scope;
    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;
    protected readonly AppDbContext DbContext;

    protected BaseIntegrationTest(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        DbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public async Task InitializeAsync()
    {
        if (_respawner == null)
        {
            await _respawnerLock.WaitAsync();
            try
            {
                if (_respawner == null)
                {
                    await using var initConn = new NpgsqlConnection(Factory.DbConnectionString);
                    await initConn.OpenAsync();
                    _respawner = await Respawner.CreateAsync(initConn, new RespawnerOptions
                    {
                        DbAdapter = DbAdapter.Postgres,
                        SchemasToInclude = [AppDbContext.Schema]
                    });
                }
            }
            finally
            {
                _respawnerLock.Release();
            }
        }

        const int maxRetries = 5;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(Factory.DbConnectionString);
                await connection.OpenAsync();
                await _respawner.ResetAsync(connection);
                break;
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" && attempt < maxRetries)
            {
                await Task.Delay(100 * attempt);
            }
        }
    }

    public Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    protected void SetTenant(Guid tenantId)
    {
        Client.DefaultRequestHeaders.Remove("X-TenantId");
        Client.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());

        TestTenantProvider.StaticTenantId = tenantId;

        // Resolve from the test's private scope so the local DbContext sees it
        // (the registered ITenantProvider is the anonymous-aware delegate; the static
        // test provider is always available as a concrete service)
        var provider = _scope.ServiceProvider.GetRequiredService<TestTenantProvider>();
        provider.CurrentTenantId = tenantId;
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
