using AlphaZero.API;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Identity.Tests.Integration.Abstractions;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16.1-alpine3.19")
        .WithDatabase("alphazero_identity_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Override database settings for all modules to use this test container
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", _dbContainer.GetConnectionString());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantProvider>();
            services.AddScoped<ITenantProvider, TestTenantProvider>();
        });
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }
}

public class TestTenantProvider : ITenantProvider
{
    public Guid? CurrentTenantId { get; set; }
    public Guid? GetTenant() => CurrentTenantId;
}
