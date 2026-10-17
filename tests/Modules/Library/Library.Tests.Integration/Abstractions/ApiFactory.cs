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

using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace Library.Tests.Integration.Abstractions;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Testing:BypassIAM", "true");
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
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
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            }).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

            services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("TestScheme").RequireAuthenticatedUser().Build());

            services.RemoveAll<ITenantProvider>();
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantProvider, TestTenantProvider>();
            
            services.RemoveAll<ICurrentTenantUserRepository>();
            services.AddScoped<ICurrentTenantUserRepository, TestCurrentTenantUserRepository>();

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

            // Suppress PendingModelChangesWarning for Library AppDbContext in test environment
            services.RemoveAll<DbContextOptions<AlphaZero.Modules.Library.Infrastructure.Persistance.AppDbContext>>();
            services.RemoveAll<AlphaZero.Modules.Library.Infrastructure.Persistance.AppDbContext>();

            var connectionString = _dbContainer.GetConnectionString();

            services.AddDbContext<AlphaZero.Modules.Library.Infrastructure.Persistance.AppDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(connectionString, h =>
                {
                    h.MigrationsAssembly(typeof(AlphaZero.Modules.Library.Infrastructure.Persistance.AppDbContext).Assembly.FullName);
                    h.MigrationsHistoryTable("__LibraryMigrationHistory", AlphaZero.Modules.Library.Infrastructure.Persistance.AppDbContext.Schema);
                });
                opts.AddInterceptors(sp.GetRequiredService<AlphaZero.Shared.Infrastructure.SoftDelete.SoftDeleteInterceptor>());
                opts.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Also suppress Courses AppDbContext which runs during global migrations
            services.RemoveAll<DbContextOptions<AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext>>();
            services.RemoveAll<AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext>();

            services.AddDbContext<AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(connectionString, h =>
                {
                    h.MigrationsAssembly(typeof(AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext).Assembly.FullName);
                    h.MigrationsHistoryTable("__CoursesMigrationHistory", AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext.Schema);
                });
                opts.AddInterceptors(sp.GetRequiredService<AlphaZero.Shared.Infrastructure.SoftDelete.SoftDeleteInterceptor>());
                opts.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }
}

public class BypassPolicyEvaluatorService : AlphaZero.Shared.Authorization.IPolicyEvaluatorService
{
    public Task<ErrorOr.ErrorOr<ErrorOr.Success>> Authorize(AlphaZero.Shared.Authorization.AuthorizationContext context) =>
        Task.FromResult<ErrorOr.ErrorOr<ErrorOr.Success>>(ErrorOr.Result.Success);
}

public class TestCurrentTenantUserRepository : ICurrentTenantUserRepository
{
    public Guid? MockUserId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid MockIdentityId { get; set; } = Guid.NewGuid();

    public Task<TenantUserDTO?> GetCurrentUser()
    {
        if (MockUserId == null) return Task.FromResult<TenantUserDTO?>(null);

        return Task.FromResult<TenantUserDTO?>(new TenantUserDTO(
            MockUserId.Value,
            MockIdentityId.ToString(),
            "Test Student"));
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
        if (context != null && context.Request.Headers.TryGetValue("X-TenantId", out var tenantIdStr))
        {
            if (Guid.TryParse(tenantIdStr, out var tenantId))
            {
                return tenantId;
            }
        }

        return CurrentTenantId;
    }
}