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

        // 3. Tenant Provider
        if (environment.IsDevelopment())
        {
            services.AddScoped<ITenantProvider, FakeTenantProvider>();
        }
        else
        {
            services.AddScoped<ITenantProvider, HttpTenantProvider>();
        }
        // 4. Common Utilities
        services.AddSingleton<IClock, Clock>();
        services.AddHttpContextAccessor();

        return services;
    }

    public static IServiceCollection AddDatabaseSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);
        services.AddSingleton(dbSettings);
        return services;
    }
}
