using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Services;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Repositories;
using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
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
        var dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);
        var awsResources = configuration.GetSection(AWSResources.Section).Get<AWSResources>() 
            ?? throw new ArgumentException("AWS Resources are not configured");

        // Infrastructure Services that need to be global for consumers
        services.AddScoped<IUploadService, S3UploadService>();
        services.AddScoped<IVideoSpecificationExtractorService, S3VideoSpecificationExtractor>();
        services.AddScoped<IVideoTranscodingService, MediaConvertTranscodingService>();
        services.AddScoped<IVideoCdnSyncService, S3VideoCdnSyncService>();

        // Persistence
        services.AddDbContext<AppDbContext>(opts =>
        {
            opts.UseNpgsql(dbSettings.ConnectionString, h=>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__VideoUploadingMigrationHistory", AppDbContext.Schema);
            });
        });

        // Special singleton for S3Settings from global configuration
        services.AddSingleton<S3Settings>(awsResources.InputS3 ?? throw new ArgumentException("S3 Input is not configured"));
    }
    public static void AddVideoUploadingPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        var awsOptions = configuration.GetAWSOptions();
        moduleServices.AddFluentValidation(typeof(IVideoUploadingApplicationMarker));

        // Application Services
        moduleServices.AddMediatR(opts => { 
            opts.RegisterServicesFromAssembly(typeof(IVideoUploadingApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        // Module Specific Infrastructure
        moduleServices.AddScoped<IVideoRepository, VideoRepository>();
        moduleServices.AddScoped<IVideoStateRepository, VideoStateRepository>();
        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
        
    }
}
