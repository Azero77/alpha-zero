using AlphaZero.Modules.VideoUploading.Application.Commands.FailVideoProcessing;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using Aspire.Shared;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class SQSVideoProcessingFailedConsumer : IConsumer<VideoProcessingFailedQueueMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<SQSVideoProcessingFailedConsumer> _logger;

    public SQSVideoProcessingFailedConsumer(
        IMediator mediator,
        ILogger<SQSVideoProcessingFailedConsumer> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProcessingFailedQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSVideoProcessingFailedConsumer triggered!");
        var msg = context.Message;
        string reason = msg.Error?.Cause ?? msg.Error?.ErrorType ?? "Unknown Step Functions failure";
        _logger.LogError("[SQS] Processing video failed callback for Video {VideoId}. Reason: {Reason}", msg.VideoId, reason);

        await _mediator.Send(new FailVideoProcessingCommand(msg.VideoId, reason), context.CancellationToken);
    }
}

public class SQSVideoProcessingFailedConsumerDefinition : ConsumerDefinition<SQSVideoProcessingFailedConsumer>
{
    public SQSVideoProcessingFailedConsumerDefinition(AWSResources resources)
    {
        var queueUrl = resources.VideoFailedQueue?.QueueUrl ?? throw new ArgumentException("Video Published Queue is not Configured");

        EndpointName = !string.IsNullOrEmpty(queueUrl) ? queueUrl.Split('/').Last() : "VideoProcessingFailedQueue";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SQSVideoProcessingFailedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}
