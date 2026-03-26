using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure;
using Amazon.MediaConvert;
using Amazon.S3;
using Amazon.SQS;
using Aspire.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AlphaZero.Modules.VideoUploading.Infrastructure;

public static class DependencyInjection
{
    public static void AddVideoUploadingGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Global AWS services
        services.AddAWSService<IAmazonSQS>();
        services.AddAWSService<IAmazonMediaConvert>();
    }

    public static void AddVideoUploadingPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        AWSResources awsResources = configuration.GetSection(AWSResources.Section).Get<AWSResources>() 
            ?? throw new ArgumentException("AWS Resources are not configured");
        DatabaseSettings dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>() 
            ?? throw new ArgumentException("Database settings are not configured");

        var awsOptions = configuration.GetAWSOptions();
        moduleServices.AddFluentValidation(typeof(IVideoUploadingApplicationMarker));

        // Application Services
        moduleServices.AddMediatR(opts => { 
            opts.RegisterServicesFromAssembly(typeof(IVideoUploadingApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        
        // Infrastructure Services
        moduleServices.AddSingleton<IAmazonS3>(sp => awsOptions.CreateServiceClient<IAmazonS3>());
        moduleServices.AddSingleton<S3Settings>(awsResources.InputS3 ?? throw new ArgumentException("S3 Input is not configured"));
        moduleServices.AddScoped<IUploadService, S3UploadService>();

        // Persistence
        moduleServices.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString);
        });
    }
}
