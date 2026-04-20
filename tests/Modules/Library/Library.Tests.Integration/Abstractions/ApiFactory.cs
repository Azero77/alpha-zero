using AlphaZero.API;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Library.Tests.Integration.Abstractions;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16.1-alpine3.19")
        .WithDatabase("alphazero_library_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", _dbContainer.GetConnectionString());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantProvider>();
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantProvider, TestTenantProvider>();
            
            services.RemoveAll<ICurrentTenantUserRepository>();
            services.AddScoped<ICurrentTenantUserRepository, TestCurrentTenantUserRepository>();
        });
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }
}

public class TestCurrentTenantUserRepository : ICurrentTenantUserRepository
{
    public Guid? MockUserId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid MockIdentityId { get; set; } = Guid.NewGuid();
    public Guid MockSessionId { get; set; } = Guid.NewGuid();

    public Task<TenantUserDTO?> GetCurrentUser()
    {
        if (MockUserId == null) return Task.FromResult<TenantUserDTO?>(null);

        return Task.FromResult<TenantUserDTO?>(new TenantUserDTO(
            MockUserId.Value,
            MockIdentityId.ToString(),
            "Test Student",
            MockSessionId));
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