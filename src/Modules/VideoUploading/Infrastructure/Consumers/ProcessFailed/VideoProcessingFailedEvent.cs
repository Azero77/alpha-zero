using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Commands.MarkVideoAsFailed;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers.ProcessFailed;

public class VideoProcessingFailedEvent(IVideoUploadingModule module, ILogger<VideoProcessingFailedEvent> logger) : IConsumer<IntegrationEvents.VideoProcessingFailedEvent>
{
    public async Task Consume(ConsumeContext<IntegrationEvents.VideoProcessingFailedEvent> context)
    {
        if (context.Message.VideoId == Guid.Empty)
        {
            logger.LogWarning("Received failure event for empty VideoId. Key: {Key}, Reason: {Reason}", 
                context.Message.Key, context.Message.Reason);
            return;
        }

        logger.LogInformation("Infrastructure: Processing failed for Video {VideoId}. Marking as FAILED. Reason: {Reason}", 
            context.Message.VideoId, context.Message.Reason);

        var result = await module.Send<MarkVideoAsFailedCommand, ErrorOr<Success>>(
            new MarkVideoAsFailedCommand(context.Message.VideoId, context.Message.Reason), 
            context.CancellationToken);

        if (result.IsError)
        {
            logger.LogError("Failed to mark video {VideoId} as FAILED: {Error}", 
                context.Message.VideoId, result.FirstError.Description);
        }
    }
}
