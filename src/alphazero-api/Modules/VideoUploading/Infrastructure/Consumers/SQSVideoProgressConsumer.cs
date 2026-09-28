using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.Application.Commands.UpdateVideoProgress;
using AlphaZero.Modules.VideoUploading.Application.Models;
using Aspire.Shared;
using MediatR;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class SQSVideoProgressConsumer : IConsumer<VideoProgressQueueMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<SQSVideoProgressConsumer> _logger;

    public SQSVideoProgressConsumer(
        IMediator mediator,
        ILogger<SQSVideoProgressConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProgressQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSVideoProgressConsumer triggered!");
        var msg = context.Message;
        _logger.LogInformation("[SQS] Video {VideoId} progress: {Stage} - {Status}",
            msg.VideoId, msg.Stage, msg.Status);

        // Try mapping the incoming string stage to our PipelineStage enum
        if (!Enum.TryParse<PipelineStage>(msg.Stage, true, out var incomingStage))
        {
            _logger.LogWarning("[SQS] Unrecognized pipeline stage: {Stage}", msg.Stage);
        }
        else
        {
            if (Guid.TryParse(msg.VideoId, out Guid videoIdGuid) && Guid.TryParse(msg.TenantId, out Guid tenantIdGuid))
            {
                await _mediator.Send(new UpdateVideoProgressCommand(
                    videoIdGuid,
                    tenantIdGuid,
                    incomingStage,
                    msg.Status,
                    msg.Metadata), context.CancellationToken);
            }
            else
            {
                _logger.LogWarning("[SQS] Invalid VideoId or TenantId Guid format: {VideoId}, {TenantId}", msg.VideoId, msg.TenantId);
            }
        }
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
