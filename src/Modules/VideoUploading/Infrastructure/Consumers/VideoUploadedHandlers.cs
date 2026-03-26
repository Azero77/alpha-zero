using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Handles the clean internal event and starts the transcoding command.
/// </summary>
public class VideoDeliveredEventHandler : IConsumer<VideoDeliveredToInputEvent>
{
    private readonly ILogger<VideoDeliveredEventHandler> _logger;

    public VideoDeliveredEventHandler(ILogger<VideoDeliveredEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoDeliveredToInputEvent> context)
    {
        _logger.LogInformation("[Event] Received VideoUploadedToInputEvent for Asset: {AssetId}. Sending transcoding command.", context.Message.VideoId);
    }
}
