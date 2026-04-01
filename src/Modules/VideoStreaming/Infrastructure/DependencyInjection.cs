using AlphaZero.Modules.VideoStreaming.Application.Queries;
using Amazon.S3;
using Application;
using Aspire.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Aspire.Shared.AWSResources;

namespace Infrastructure;

public static class DependencyInjection
{
    public static void AddVideoStreamingInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddMediatR(opts => opts.RegisterServicesFromAssembly(typeof(IVideoStreamingApplicationMarker).Assembly));
        services.AddSingleton<IStreamingService, S3VideoStreamingService>();
    }
}
