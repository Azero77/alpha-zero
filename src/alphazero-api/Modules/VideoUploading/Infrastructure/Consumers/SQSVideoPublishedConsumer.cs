using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;


public class SQSVideoPublishedConsumer : IConsumer<VideoPublishedQueueMessage>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IModuleBus _moduleBus;
    private readonly IClock _clock;
    private readonly IVideoProgressNotifier _progressNotifier;
    private readonly ILogger<SQSVideoPublishedConsumer> _logger;

    public SQSVideoPublishedConsumer(
        IVideoRepository videoRepository,
        IUnitOfWork unitOfWork,
        IModuleBus moduleBus,
        IClock clock,
        IVideoProgressNotifier progressNotifier,
        ILogger<SQSVideoPublishedConsumer> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _moduleBus = moduleBus;
        _clock = clock;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedQueueMessage> context)
    {
        _logger.LogInformation("[SQS] SQSVideoPublishedConsumer triggered!");
        var msg = context.Message;
        _logger.LogInformation("[SQS] Processing video published callback for Video: {VideoId}", msg.VideoId);

        var video = await _videoRepository.GetByIdAsync(msg.VideoId, context.CancellationToken);
        if (video is null)
        {
            _logger.LogWarning("[SQS] Video {VideoId} not found in database. Skipping.", msg.VideoId);
            return;
        }

        // Idempotency check: if already published, skip duplicate processing
        if (video.Status == VideoStatus.Published)
        {
            _logger.LogInformation("[SQS] Video {VideoId} is already marked as Published. Skipping.", msg.VideoId);
            return;
        }

        TimeSpan duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(msg.Duration) && TimeSpan.TryParse(msg.Duration, out var parsedDuration))
        {
            duration = parsedDuration;
        }

        var resolution = (msg.Width.HasValue && msg.Height.HasValue)
            ? new Resolution(msg.Width.Value, msg.Height.Value)
            : Resolution.Empty;

        video.UpdateSpecifications(new VideoSpecifications(duration, resolution));
        video.MarkAsLive(msg.PlaybackUrl, _clock);

        await _unitOfWork.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("[SQS] Successfully published Video {VideoId}. Playback URL: {Url}", msg.VideoId, msg.PlaybackUrl);

        // Notify other modules (e.g. Courses module)
        await _moduleBus.Publish(new VideoPublishedEvent(
            video.Id,
            msg.PlaybackUrl,
            msg.TargetResourceArn), context.CancellationToken);

        // Notify SignalR clients that publishing is complete
        await _progressNotifier.NotifyProgressAsync(new VideoProgressNotification(
            msg.VideoId.ToString(),
            msg.TenantId.ToString(),
            "publishing",
            "COMPLETE",
            null
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
