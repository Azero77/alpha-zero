using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Commands.PublishVideo;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using Aspire.Shared;
using MediatR;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class SQSVideoPublishedConsumer : IConsumer<VideoPublishedQueueMessage>
{
    private readonly IVideoUploadingModule _module;
    private readonly ILogger<SQSVideoPublishedConsumer> _logger;

    public SQSVideoPublishedConsumer(
        IVideoUploadingModule module,
        ILogger<SQSVideoPublishedConsumer> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSVideoPublishedConsumer triggered!");
        var msg = context.Message;
        _logger.LogInformation("[SQS] Processing video published callback for Video: {VideoId}", msg.VideoId);

        await _module.Send(new CompleteVideoPublishingCommand(
            msg.VideoId,
            msg.TenantId,
            msg.PlaybackUrl,
            msg.Duration,
            msg.Width,
            msg.Height,
            msg.TargetResourceArn
        ), context.CancellationToken);
    }
}

public class SQSVideoPublishedConsumerDefinition : ConsumerDefinition<SQSVideoPublishedConsumer>
{
    public SQSVideoPublishedConsumerDefinition(AWSResources resources)
    {
        var queueUrl = resources.VideoPublishedQueue?.QueueUrl ?? throw new ArgumentException("Video Published Queue is not Configured");
        EndpointName = !string.IsNullOrEmpty(queueUrl) ? queueUrl.Split('/').Last() : "VideoPublishedQueue";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<SQSVideoPublishedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}
