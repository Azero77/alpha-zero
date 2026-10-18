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

    /// <summary>
    /// Connection string of this factory's test database. Secondary hosts (e.g. the IAM-enforcing
    /// security factory) reuse it via InitializeSharedDatabaseAsync instead of starting a second
    /// container — a second container plus the process-global connection env vars caused mid-run
    /// 57P01 connection terminations across the main factory's tests.
    /// </summary>
    public string DbConnectionString => _dbContainer.GetConnectionString();

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16.1-alpine3.19")
        .WithDatabase("alphazero_courses_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public WireMockServer MockServer { get; private set; } = null!;

    /// <summary>
    /// Whether this factory owns (starts/stops) a dedicated Postgres container. Secondary factories
    /// (e.g. the IAM-enforcing security factory) share the primary factory's database instead — a
    /// second container plus the process-global connection env vars caused mid-run 57P01
    /// connection terminations across the main factory's tests.
    /// </summary>
    protected virtual bool OwnsDatabase => true;

    public virtual async Task InitializeAsync()
    {
        if (OwnsDatabase)
        {
            await _dbContainer.StartAsync();

            // Use environment variables to override settings BEFORE builder.Build() is called in Program.cs
            Environment.SetEnvironmentVariable("ConnectionStrings__alphazerodb", _dbContainer.GetConnectionString());
            Environment.SetEnvironmentVariable("DatabaseSettings__ConnectionString", _dbContainer.GetConnectionString());
        }

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

            // Replace Tenant Provider
            services.RemoveAll<ITenantProvider>();
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantProvider, TestTenantProvider>();

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

            // Ensure IPublishEndpoint publishes to in-memory IBus rather than external SQS bus in tests.
            // RecordingPublishObserver captures module-published payloads for lifecycle-event assertions (#34).
            services.AddScoped<MassTransit.IPublishEndpoint>(sp => sp.GetRequiredService<MassTransit.IBus>());
        });
    }

    public virtual async Task DisposeAsync()
    {
        if (OwnsDatabase)
        {
            await _dbContainer.StopAsync();
        }
        MockServer.Stop();
    }
}

public class TestTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
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

    public TestTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
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

        return CurrentTenantId ?? StaticTenantId;
    }
}

public class BypassPolicyEvaluatorService : AlphaZero.Shared.Authorization.IPolicyEvaluatorService
{
    public Task<ErrorOr.ErrorOr<ErrorOr.Success>> Authorize(AlphaZero.Shared.Authorization.AuthorizationContext context) =>
        Task.FromResult<ErrorOr.ErrorOr<ErrorOr.Success>>(ErrorOr.Result.Success);
}
