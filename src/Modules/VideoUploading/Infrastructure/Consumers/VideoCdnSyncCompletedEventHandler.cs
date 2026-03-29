using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Commands.MarkVideoAsLive;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class VideoCdnSyncCompletedEventHandler : IConsumer<VideoCdnSyncCompletedEvent>
{
    private readonly ILogger<VideoCdnSyncCompletedEventHandler> _logger;
    private readonly IVideoUploadingModule _module;

    public VideoCdnSyncCompletedEventHandler(
        ILogger<VideoCdnSyncCompletedEventHandler> logger, 
        IVideoUploadingModule module)
    {
        _logger = logger;
        _module = module;
    }

    public async Task Consume(ConsumeContext<VideoCdnSyncCompletedEvent> context)
    {
        _logger.LogInformation("Infrastructure: CDN Sync complete for Video {VideoId}. Publishing video.", context.Message.VideoId);

        var result = await _module.Send<MarkVideoAsLiveCommand, ErrorOr<Success>>(
            new MarkVideoAsLiveCommand(context.Message.VideoId, context.Message.R2PublicUrl), 
            context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Failed to mark video as LIVE for {VideoId}: {Error}", 
                context.Message.VideoId, result.FirstError.Description);
            
            await context.Publish(new VideoProcessingFailedEvent(
                context.Message.VideoId, 
                $"Database update failed after CDN sync: {result.FirstError.Description}", 
                null));
            return;
        }

        // Final event to finalize the Saga
        await context.Publish(new VideoPublishedEvent(context.Message.VideoId, context.Message.R2PublicUrl));
    }
}
