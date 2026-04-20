using AlphaZero.API;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace Courses.Tests.Integration.Abstractions;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16.1-alpine3.19")
        .WithDatabase("alphazero_courses_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public WireMockServer MockServer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Use environment variables to override settings BEFORE builder.Build() is called in Program.cs
        Environment.SetEnvironmentVariable("ConnectionStrings__alphazerodb", _dbContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", _dbContainer.GetConnectionString());

        MockServer = WireMockServer.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            // Replace Tenant Provider
            services.RemoveAll<ITenantProvider>();
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantProvider, TestTenantProvider>();
        });
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        MockServer.Stop();
    }
}

public class TestTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public Guid? CurrentTenantId { get; set; }

    public TestTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetTenant()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null && context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdStr))
        {
            if (Guid.TryParse(tenantIdStr, out var tenantId))
            {
                return tenantId;
            }
        }

        return CurrentTenantId;
    }
}
