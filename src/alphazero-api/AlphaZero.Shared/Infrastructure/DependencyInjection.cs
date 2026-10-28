using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Aspire.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.S3;
using Amazon.SQS;
using Amazon.MediaConvert;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using AlphaZero.Shared.Infrastructure.Caching;
using AlphaZero.Shared.Infrastructure.Messaging;
using Microsoft.Extensions.Caching.Hybrid;

namespace AlphaZero.Shared.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // 1. AWS Resources & Options
        var awsResources = new AWSResources();
        configuration.Bind(AWSResources.Section, awsResources);
        services.AddSingleton(awsResources);
        
        var awsOptions = configuration.GetAWSOptions();
        // Infrastructure Services that need to be global for consumers
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddDefaultAWSOptions(awsOptions);

        // 2. Common AWS Service Clients (Global)
        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonSQS>();
        services.AddAWSService<IAmazonMediaConvert>();
        services.AddAWSService<Amazon.SimpleSystemsManagement.IAmazonSimpleSystemsManagement>();

        // 3. Tenant Provider
        if (environment.IsDevelopment())
        {
            services.AddScoped<ITenantProvider, HttpTenantProvider>();
        }
        else
        {
            services.AddScoped<ITenantProvider, HttpTenantProvider>();
        }
        // 4. Common Utilities
        services.AddSingleton<IClock, Clock>();
        services.AddSingleton<IAmbientTenantContext, AmbientTenantContext>();
        services.AddSingleton(typeof(TenantConsumeFilter<>));
        services.AddSingleton(typeof(TenantPublishFilter<>));
        services.AddScoped<ICachingService, HybridCachingService>();
        services.AddScoped<IEvictionProvider, ScopedEvictionProvider>();
        // Eviction flush interceptors: the queue lives ON the request-scoped ScopedEvictionProvider
        // instance, and every participant in a write (repository decorators, unit of work,
        // DbContext, both interceptors) resolves from the same lifetime scope, so the flush sees
        // the lanes queued by the handlers. They are no-ops when the queue is empty, so adding
        // them to other module DbContexts later is safe.
        services.AddScoped<EvictionSaveChangesInterceptor>();
        services.AddScoped<EvictionTransactionInterceptor>();
        services.AddHttpContextAccessor();

        // 5. Cloudflare Edge Security & Playback Ticketing
        services.Configure<AlphaZero.Shared.Security.CloudflareSettings>(configuration.GetSection(AlphaZero.Shared.Security.CloudflareSettings.SectionName));
        var playbackTicketSecret = configuration.GetSection(AlphaZero.Shared.Security.CloudflareSettings.SectionName)[nameof(AlphaZero.Shared.Security.CloudflareSettings.PlaybackTicketSecret)];
        if (string.IsNullOrWhiteSpace(playbackTicketSecret) && !environment.IsDevelopment())
        {
            // Fail fast: a missing playback-ticket secret must never silently degrade to the dev constant
            // (forgeable tickets). Development keeps the dev default; production requires an explicit secret.
            throw new InvalidOperationException(
                $"Missing required configuration '{AlphaZero.Shared.Security.CloudflareSettings.SectionName}:{nameof(AlphaZero.Shared.Security.CloudflareSettings.PlaybackTicketSecret)}'. " +
                "Playback tickets cannot be minted securely without an explicit secret in non-development environments.");
        }
        services.AddSingleton<AlphaZero.Shared.Security.ICloudflareCookieSigner, AlphaZero.Shared.Infrastructure.Security.CloudflareCookieSigner>();
        services.AddSingleton<AlphaZero.Shared.Security.IPlaybackTicketSigner, AlphaZero.Shared.Infrastructure.Security.PlaybackTicketSigner>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddHybridCache(o =>
        {
            o.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            };
        });
        
        services.AddOptions<R2Options>()
            .BindConfiguration(R2Options.SectionName)
            .ValidateOnStart();
        
        return services;
    }

    public static IServiceCollection AddDatabaseSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);
        services.AddSingleton(dbSettings);
        return services;
    }
}
