using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.Application.Services;
using Aspire.Shared;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;


public class SQSVideoProgressConsumer : IConsumer<VideoProgressQueueMessage>
{
    private readonly IVideoProgressNotifier _progressNotifier;
    private readonly AlphaZero.Modules.VideoUploading.Infrastructure.Persistance.AppDbContext _dbContext;
    private readonly ILogger<SQSVideoProgressConsumer> _logger;

    public SQSVideoProgressConsumer(
        IVideoProgressNotifier progressNotifier,
        AlphaZero.Modules.VideoUploading.Infrastructure.Persistance.AppDbContext dbContext,
        ILogger<SQSVideoProgressConsumer> logger)
    {
        _progressNotifier = progressNotifier;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProgressQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSVideoProgressConsumer triggered!");
        var msg = context.Message;
        _logger.LogInformation("[SQS] Video {VideoId} progress: {Stage} - {Status}",
            msg.VideoId, msg.Stage, msg.Status);

        // Try mapping the incoming string stage to our PipelineStage enum
        if (!Enum.TryParse<AlphaZero.Modules.VideoUploading.Application.Models.PipelineStage>(msg.Stage, true, out var incomingStage))
        {
            // If it doesn't map cleanly, we still send the SignalR notification, but we can't do idempotency checks
            _logger.LogWarning("[SQS] Unrecognized pipeline stage: {Stage}", msg.Stage);
        }
        else
        {
            if (Guid.TryParse(msg.VideoId, out var videoIdGuid))
            {
                var videoState = await _dbContext.VideoState.FirstOrDefaultAsync(s => s.VideoId == videoIdGuid, context.CancellationToken);
                if (videoState != null)
                {
                    // Idempotency check: drop stale or duplicate messages
                if ((int)incomingStage <= (int)videoState.Stage)
                {
                    _logger.LogInformation("[SQS] Stale or duplicate progress message for Video {VideoId}: incoming {IncomingStage} <= current {CurrentStage}. Ignoring DB update.", msg.VideoId, incomingStage, videoState.Stage);
                }
                else
                {
                    videoState.Stage = incomingStage;
                    await _dbContext.SaveChangesAsync(context.CancellationToken);
                    _logger.LogInformation("[SQS] Updated VideoState for Video {VideoId} to {Stage}", msg.VideoId, incomingStage);
                }
            }
        }
        }

        await _progressNotifier.NotifyProgressAsync(
            new VideoProgressNotification(msg.VideoId.ToString(), msg.TenantId.ToString(), msg.Stage, msg.Status, msg.Metadata),
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
