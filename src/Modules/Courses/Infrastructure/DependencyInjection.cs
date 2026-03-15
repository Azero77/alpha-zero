using Amazon.S3;
using Application;
using Aspire.Shared;
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
        AWSResources awsResources = configuration.GetValue<AWSResources>(AWSResources.Section) ?? throw new ArgumentException("AWS Resources are not configured in VideoStreaming module");
        services.AddSingleton<S3Settings>(awsResources.s3 ?? throw new ArgumentException("S3 is not configured in video streaming"));

    }
}
