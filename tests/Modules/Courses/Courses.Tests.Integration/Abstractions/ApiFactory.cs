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
        Environment.SetEnvironmentVariable("Testing:BypassIAM", "true");
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
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

            services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("TestScheme").RequireAuthenticatedUser().Build());

            // Replace Tenant Provider. The anonymous opt-out path (#32/D4) runs the REAL
            // HttpTenantProvider so claim/header precedence (D2) is exercised end-to-end;
            // every other request keeps the static TestTenantProvider — existing tests
            // are unchanged.
            services.RemoveAll<ITenantProvider>();
            services.AddHttpContextAccessor();
            services.AddScoped<TestTenantProvider>();
            services.AddScoped<HttpTenantProvider>();
            services.AddScoped<ITenantProvider>(sp =>
            {
                var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
                return httpContext?.Request.Headers.ContainsKey("X-Test-Anonymous") == true
                    ? (ITenantProvider)sp.GetRequiredService<HttpTenantProvider>()
                    : sp.GetRequiredService<TestTenantProvider>();
            });

            // Bypass IAM evaluation for integration tests when Testing:BypassIAM is set
            if (Environment.GetEnvironmentVariable("Testing:BypassIAM") == "true")
            {
                services.RemoveAll<AlphaZero.Shared.Authorization.IPolicyEvaluatorService>();
                services.AddScoped<AlphaZero.Shared.Authorization.IPolicyEvaluatorService, BypassPolicyEvaluatorService>();
            }

            // Mock AWSResources so MassTransit SQS consumers can instantiate
            services.AddSingleton(new Aspire.Shared.AWSResources 
            { 
                VideoProgressQueue = new Aspire.Shared.SQSQueueSettings { QueueUrl = "https://sqs.mock/mock-queue" }, 
                VideoFailedQueue = new Aspire.Shared.SQSQueueSettings { QueueUrl = "https://sqs.mock/mock-queue-failed" }, 
                VideoPublishedQueue = new Aspire.Shared.SQSQueueSettings { QueueUrl = "https://sqs.mock/mock-queue-pub" },
                DocumentUploadedQueue = new Aspire.Shared.SQSQueueSettings { QueueUrl = "https://sqs.mock/mock-queue-doc" },
                DocumentProcessingCompletedQueue = new Aspire.Shared.SQSQueueSettings { QueueUrl = "https://sqs.mock/mock-queue-doc-completed" },
                DocumentProcessingFailedQueue = new Aspire.Shared.SQSQueueSettings { QueueUrl = "https://sqs.mock/mock-queue-doc-failed" }
            });

            // Ensure IPublishEndpoint publishes to in-memory IBus rather than external SQS bus in tests
            services.AddScoped<MassTransit.IPublishEndpoint>(sp => sp.GetRequiredService<MassTransit.IBus>());
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
    private readonly IAmbientTenantContext _ambientTenant;
    private static readonly AsyncLocal<Guid?> _asyncLocalTenantId = new();
    private static Guid? _fallbackTenantId;

    public static Guid? StaticTenantId
    {
        get => _asyncLocalTenantId.Value ?? _fallbackTenantId;
        set
        {
            _asyncLocalTenantId.Value = value;
            _fallbackTenantId = value;
        }
    }

    public Guid? CurrentTenantId
    {
        get => _asyncLocalTenantId.Value ?? _fallbackTenantId;
        set
        {
            _asyncLocalTenantId.Value = value;
            _fallbackTenantId = value;
        }
    }

    public TestTenantProvider(IHttpContextAccessor httpContextAccessor, IAmbientTenantContext ambientTenant)
    {
        _httpContextAccessor = httpContextAccessor;
        _ambientTenant = ambientTenant;
    }

    public Guid? GetTenant()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null && context.Request.Headers.TryGetValue("X-TenantId", out var tenantIdStr))
        {
            if (Guid.TryParse(tenantIdStr, out var tenantId))
            {
                return tenantId;
            }
        }

        // Consumers have no HTTP context; the consume filter sets this. It must win over the
        // static fallback so a genuinely tenant-less bus scope is observable in tests.
        if (_ambientTenant.TenantId is { } ambientTenantId)
            return ambientTenantId;

        return CurrentTenantId ?? StaticTenantId;
    }
}

public class BypassPolicyEvaluatorService : AlphaZero.Shared.Authorization.IPolicyEvaluatorService
{
    public Task<ErrorOr.ErrorOr<ErrorOr.Success>> Authorize(AlphaZero.Shared.Authorization.AuthorizationContext context) =>
        Task.FromResult<ErrorOr.ErrorOr<ErrorOr.Success>>(ErrorOr.Result.Success);
}
