using AlphaZero.Modules.VideoUploading.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public record VideoProgressQueueMessage(
    string VideoId,
    string TenantId,
    string Stage,
    string Status,
    int? Percentage = null,
    string? Metadata = null);

public class SQSVideoProgressConsumer : IConsumer<VideoProgressQueueMessage>
{
    private readonly IVideoProgressNotifier _progressNotifier;
    private readonly ILogger<SQSVideoProgressConsumer> _logger;

    public SQSVideoProgressConsumer(
        IVideoProgressNotifier progressNotifier,
        ILogger<SQSVideoProgressConsumer> logger)
    {
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProgressQueueMessage> context)
    {
        var msg = context.Message;
        _logger.LogInformation("[SQS] Video {VideoId} progress: {Stage} - {Status} ({Percentage}%)",
            msg.VideoId, msg.Stage, msg.Status, msg.Percentage);

        await _progressNotifier.NotifyProgressAsync(
            new VideoProgressNotification(msg.VideoId, msg.TenantId, msg.Stage, msg.Status, msg.Percentage, msg.Metadata),
            context.CancellationToken);
    }
}
