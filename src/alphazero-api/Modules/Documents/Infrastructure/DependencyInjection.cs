using AlphaZero.Modules.Documents.Application;
using AlphaZero.Modules.Documents.Application.Metadata;
using AlphaZero.Modules.Documents.Application.Models;
using AlphaZero.Modules.Documents.Application.Services;
using AlphaZero.Modules.Documents.Domain.Repositories;
using AlphaZero.Modules.Documents.Infrastructure.Persistance;
using AlphaZero.Modules.Documents.Infrastructure.Repositories;
using AlphaZero.Modules.Documents.Infrastructure.Services;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Infrastructure.SoftDelete;
using Amazon.S3;
using Aspire.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace AlphaZero.Modules.Documents.Infrastructure;

public static class DependencyInjection
{
    public static void AddDocumentsGlobalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = DatabaseSettings.GetDatabaseSettings(configuration);

        var dataSource = new NpgsqlDataSourceBuilder(dbSettings.ConnectionString)
            .EnableDynamicJson()
            .Build();

        AddStorageServices(services);
        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(dataSource, h =>
            {
                h.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                h.MigrationsHistoryTable("__DocumentsMigrationHistory", AppDbContext.Schema);
            });

            opts.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });
    }

    private static void AddStorageServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IDocumentStorageService, DocumentStorageService>(StorageType.InputPrivate, (sp,key) =>
        {
            var bucketName = sp.GetRequiredService<AWSResources>()?.DocumentInputS3Private?.BucketName;
            var s3Serivce = sp.GetRequiredService<IAmazonS3>();
            ArgumentNullException.ThrowIfNull(bucketName);
            
            return new DocumentStorageService(s3Serivce,bucketName);
        });
        
        services.AddKeyedScoped<IDocumentStorageService, DocumentStorageService>(StorageType.InputPublic, (sp, _) =>
        {
            var bucketName = sp.GetRequiredService<AWSResources>()?.DocumentInputS3Public?.BucketName;
            ArgumentNullException.ThrowIfNull(bucketName);
            var s3Serivce = sp.GetRequiredService<IAmazonS3>();
            return new DocumentStorageService(s3Serivce,bucketName);
        });
        
        
        services.AddKeyedSingleton<IAmazonS3>("R2", (sp, _) =>
        {
            var options = sp
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<R2Options>>().Value;

            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceURL,
                AuthenticationRegion = "auto",
                ForcePathStyle = true
            };

            return new AmazonS3Client(
                options.AccessKeyId,
                options.SecretAccessKey,
                config);
        });
        
        services.AddKeyedScoped<IDocumentStorageService, DocumentStorageService>(StorageType.OutputPrivate, (sp, _) =>
        {
            var r2Options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<R2Options>>().Value;
            ArgumentNullException.ThrowIfNull(r2Options);
            
            var bucketName = r2Options?.PrivateBucketName;
            ArgumentNullException.ThrowIfNull(bucketName); 
         
            var r2Client = sp.GetRequiredKeyedService<IAmazonS3>("R2");

            return new DocumentStorageService(r2Client,bucketName);
        });
        
        
        services.AddKeyedScoped<IDocumentStorageService, DocumentStorageService>(StorageType.OutputPublic, (sp, _) =>
        {
            var r2Options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<R2Options>>().Value;
            ArgumentNullException.ThrowIfNull(r2Options);
            
            var bucketName = r2Options?.PublicBucketName;
            ArgumentNullException.ThrowIfNull(bucketName); 

            var r2Client = sp.GetRequiredKeyedService<IAmazonS3>("R2");
            return new DocumentStorageService(r2Client,bucketName);
        });
    }

    public static void AddDocumentsPrivateInfrastructure(this IServiceCollection moduleServices, IConfiguration configuration)
    {
        moduleServices.AddFluentValidation(typeof(IDocumentsApplicationMarker));

        moduleServices.AddMediatR(opts =>
        {
            opts.RegisterServicesFromAssembly(typeof(IDocumentsApplicationMarker).Assembly);
            opts.AddOpenBehavior(typeof(ValidationBehavior<,>));
            opts.AddOpenBehavior(typeof(UnitOfWorkDecoratorCommandHandler<,>));
        });

        moduleServices.AddScoped<IDocumentRepository, DocumentRepository>();
        moduleServices.AddScoped<IUnitOfWork, UnitOfWork<AppDbContext>>();
        
        moduleServices.AddScoped<IDocumentMetadataPipeline, DocumentMetadataPipeline>();
        // Add specific metadata sources here when implemented
        // moduleServices.AddScoped<IDocumentMetadataSource, ImageMetadataSource>();
    }
}
