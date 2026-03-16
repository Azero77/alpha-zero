using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Application;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Aspire.Shared.AWSResources;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddCoursesInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {

        var awsOptions = configuration.GetAWSOptions(); 
        services.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(ICoursesApplicationMarker).Assembly));
        services.AddSingleton<IAmazonS3>(sp => awsOptions.CreateServiceClient<IAmazonS3>());
        AWSResources awsResources = configuration.GetSection(AWSResources.Section).Get<AWSResources>() ?? throw new ArgumentException("AWS Resources are not configured in Courses module");
        services.AddSingleton<S3Settings>(awsResources.s3 ?? throw new ArgumentException("S3 is not configured in Courses module"));
        services.AddSingleton<IUploadService, S3UploadService>();
        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(DependencyInjection).Assembly);
            x.UsingAmazonSqs((context,cfg) =>
            {
                if (awsOptions.Region != null)
                {
                    cfg.Host(awsOptions.Region.SystemName, s => { });
                }
                cfg.ConfigureEndpoints(context);
                cfg.ReceiveEndpoint(awsResources.VideoUploadedQueue?.QueueUrl ?? "",e =>
                {
                    e.ConfigureConsumer<VideoUploadedConsumer>(context);
                });
            });
        });

    }
}
