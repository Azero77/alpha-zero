using AlphaZero.API;
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

namespace Identity.Tests.Integration.Abstractions;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
    }

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16.1-alpine3.19")
        .WithDatabase("alphazero_identity_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DatabaseSettings:ConnectionString", _dbContainer.GetConnectionString());
        
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

            // Suppress PendingModelChangesWarning for AppDbContext in test environment
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<AppDbContext>();

            var connectionString = _dbContainer.GetConnectionString();

            services.AddDbContext<AppDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(connectionString, h =>
                {
                    h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    h.MigrationsHistoryTable("__CoursesMigrationHistory", AppDbContext.Schema);
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
