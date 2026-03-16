using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using AlphaZero.Modules.Courses.Infrastructure.Workers;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.S3.Util;
using Amazon.SQS;
using Application;
using Aspire.Shared;
using Autofac.Core;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Aspire.Shared.AWSResources;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddCoursesGlobalInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        AWSResources awsResources = configuration.GetSection(AWSResources.Section).Get<AWSResources>() ?? throw new ArgumentException("AWS Resources are not configured in Courses module");
        services.AddHostedService<S3VideoUploadedSQSPoller>();
        services.AddAWSService<IAmazonSQS>();

    }

    public static void AddCoursesPrivateInfrastructure(this IServiceCollection moduleServices,IConfiguration configuration)
    {
        AWSResources awsResources = configuration.GetSection(AWSResources.Section).Get<AWSResources>() ?? throw new ArgumentException("AWS Resources are not configured in Courses module");
        var awsOptions = configuration.GetAWSOptions();
        moduleServices.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(ICoursesApplicationMarker).Assembly));
        moduleServices.AddSingleton<IAmazonS3>(sp => awsOptions.CreateServiceClient<IAmazonS3>());
        moduleServices.AddSingleton<S3Settings>(awsResources.s3 ?? throw new ArgumentException("S3 is not configured in Courses module"));
        moduleServices.AddSingleton<IUploadService, S3UploadService>();
    }

}
