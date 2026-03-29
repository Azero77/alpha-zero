using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Commands.PersistVideo;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class VideoMetadataProcessedEventHandler : IConsumer<VideoMetadataProcessedEvent>
{
    private readonly ILogger<VideoMetadataProcessedEventHandler> _logger;
    private readonly IVideoUploadingModule _module;

    public VideoMetadataProcessedEventHandler(
        ILogger<VideoMetadataProcessedEventHandler> logger, 
        IVideoUploadingModule module)
    {
        _logger = logger;
        _module = module;
    }

    public async Task Consume(ConsumeContext<VideoMetadataProcessedEvent> context)
    {
        _logger.LogInformation("Infrastructure: Received metadata for Video {VideoId}. Triggering persistence.", context.Message.VideoId);

        var result = await _module.Send<PersistVideoCommand, ErrorOr<Success>>(new PersistVideoCommand(
            context.Message.VideoId,
            context.Message.Duration,
            context.Message.Width,
            context.Message.Height), context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Failed to persist video metadata for {VideoId}: {Error}", 
                context.Message.VideoId, result.FirstError.Description);
            
            await context.Publish(new VideoProcessingFailedEvent(
                context.Message.VideoId, 
                $"Persistence failed: {result.FirstError.Description}", 
                null));
            return;
        }

        // Successfully persisted, now we let the Saga know so it can move to the next stage
        // Wait, the Saga is ALREADY listening for VideoMetadataProcessedEvent! 
        // So we don't need to publish anything else here.
    }
}
