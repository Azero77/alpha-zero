using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Commands.MarkVideoAsOptimized;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class VideoTranscodingFinishedEventHandler : IConsumer<VideoTranscodingFinishedEvent>
{
    private readonly ILogger<VideoTranscodingFinishedEventHandler> _logger;
    private readonly IVideoUploadingModule _module;

    public VideoTranscodingFinishedEventHandler(
        ILogger<VideoTranscodingFinishedEventHandler> logger, 
        IVideoUploadingModule module)
    {
        _logger = logger;
        _module = module;
    }

    public async Task Consume(ConsumeContext<VideoTranscodingFinishedEvent> context)
    {
        _logger.LogInformation("Infrastructure: Transcoding finished for Video {VideoId}. Updating database.", context.Message.VideoId);

        var result = await _module.Send<MarkVideoAsOptimizedCommand, ErrorOr<Success>>(
            new MarkVideoAsOptimizedCommand(context.Message.VideoId, context.Message.OutputKeyPrefix), 
            context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Failed to mark video as optimized for {VideoId}: {Error}", 
                context.Message.VideoId, result.FirstError.Description);
            
            await context.Publish(new VideoProcessingFailedEvent(
                context.Message.VideoId, 
                $"Database update failed after transcoding: {result.FirstError.Description}", 
                context.Message.OutputKeyPrefix));
        }
    }
}
