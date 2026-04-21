using AlphaZero.Modules.VideoUploading.Domain.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class AnalyzeVideoCommandHandler : IConsumer<AnalyzeVideoCommand>
{
    private readonly IVideoSpecificationExtractorService _specExtractor;
    private readonly ILogger<AnalyzeVideoCommandHandler> _logger;

    public AnalyzeVideoCommandHandler(
        IVideoSpecificationExtractorService specExtractor, 
        ILogger<AnalyzeVideoCommandHandler> logger)
    {
        _specExtractor = specExtractor;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AnalyzeVideoCommand> context)
    {
        _logger.LogInformation("Analyzing source video for Video {VideoId}. Key: {Key}", 
            context.Message.VideoId, context.Message.Key);

        var specResult = await _specExtractor.ExtractAsync(context.Message.Key, context.CancellationToken);

        if (specResult.IsError)
        {
            _logger.LogError("Failed to analyze video {VideoId}: {Error}", 
                context.Message.VideoId, specResult.FirstError.Description);
            
            await context.Publish(new VideoProcessingFailedEvent(
                context.Message.VideoId, 
                $"Analysis failed: {specResult.FirstError.Description}",
                context.Message.Key));
            return;
        }

        var specs = specResult.Value;
        _logger.LogInformation("Analysis complete for Video {VideoId}. Dimensions: {Width}x{Height}, Duration: {Duration}", 
            context.Message.VideoId, specs.Resolution.width, specs.Resolution.height, specs.Duration);

        await context.Publish(new VideoMetadataProcessedEvent(
            context.Message.VideoId, 
            specs.Duration, 
            specs.Resolution.width, 
            specs.Resolution.height
            ));
    }
}
