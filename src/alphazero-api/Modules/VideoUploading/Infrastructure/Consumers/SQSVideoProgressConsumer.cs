using AlphaZero.Modules.VideoUploading.Application.Services;
using Aspire.Shared;
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
        _logger.LogInformation("[SQS] SQSVideoProgressConsumer triggered!");
        var msg = context.Message;
        _logger.LogInformation("[SQS] Video {VideoId} progress: {Stage} - {Status} ({Percentage}%)",
            msg.VideoId, msg.Stage, msg.Status, msg.Percentage);

        await _progressNotifier.NotifyProgressAsync(
            new VideoProgressNotification(msg.VideoId, msg.TenantId, msg.Stage, msg.Status, msg.Percentage, msg.Metadata),
            context.CancellationToken);
    }
}

public class SQSVideoProgressConsumerDefinition : ConsumerDefinition<SQSVideoProgressConsumer>
{
    public SQSVideoProgressConsumerDefinition(AWSResources resources)
    {
        var queueUrl = resources.VideoProgressQueue?.QueueUrl ?? throw new ArgumentException("Video Progress Queue is not Configured");
        EndpointName = !string.IsNullOrEmpty(queueUrl) ? queueUrl.Split('/').Last() : "VideoProcessingProgressQueue";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SQSVideoProgressConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}
