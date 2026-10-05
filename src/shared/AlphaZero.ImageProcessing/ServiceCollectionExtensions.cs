using Microsoft.Extensions.DependencyInjection;
using AlphaZero.ImageProcessing.Strategies;

namespace AlphaZero.ImageProcessing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImageProcessingStrategies(this IServiceCollection services)
    {
        services.AddTransient<IImageProcessorFactory, ImageProcessorFactory>();
        services.AddTransient<IImageProcessingStrategy, CourseCoverProcessingStrategy>();
        services.AddTransient<IImageProcessingStrategy, VideoThumbnailProcessingStrategy>();
        services.AddTransient<IImageProcessingStrategy, InlineImageProcessingStrategy>();
        services.AddTransient<IImageProcessingStrategy, AvatarProcessingStrategy>();
        return services;
    }
}
