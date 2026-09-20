using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public record VideoProcessingErrorDetail(string? ErrorType, string? Cause);

public record VideoProcessingFailedQueueMessage(
    Guid VideoId,
    Guid TenantId,
    string Status,
    VideoProcessingErrorDetail? Error,
    string? TargetResourceArn);

public class SQSVideoProcessingFailedConsumer : IConsumer<VideoProcessingFailedQueueMessage>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IModuleBus _moduleBus;
    private readonly ILogger<SQSVideoProcessingFailedConsumer> _logger;

    public SQSVideoProcessingFailedConsumer(
        IVideoRepository videoRepository,
        IUnitOfWork unitOfWork,
        IModuleBus moduleBus,
        ILogger<SQSVideoProcessingFailedConsumer> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _moduleBus = moduleBus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoProcessingFailedQueueMessage> context)
    {
        var msg = context.Message;
        string reason = msg.Error?.Cause ?? msg.Error?.ErrorType ?? "Unknown Step Functions failure";
        _logger.LogError("[SQS] Processing video failed callback for Video {VideoId}. Reason: {Reason}", msg.VideoId, reason);

        var video = await _videoRepository.GetByIdAsync(msg.VideoId, context.CancellationToken);
        if (video != null && video.Status != VideoStatus.Failed)
        {
            video.MarkAsFailed();
            await _unitOfWork.SaveChangesAsync(context.CancellationToken);
        }

        // Notify other modules (e.g. Courses module)
        await _moduleBus.Publish(new VideoProcessingFailedEvent(
            msg.VideoId,
            reason,
            null,
            msg.TargetResourceArn), context.CancellationToken);
    }
}

public class SQSVideoProcessingFailedConsumerDefinition : ConsumerDefinition<SQSVideoProcessingFailedConsumer>
{
    public SQSVideoProcessingFailedConsumerDefinition()
    {
        EndpointName = "VideoProcessingFailedQueue";
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
