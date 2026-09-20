using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public record VideoPublishedQueueMessage(
    Guid VideoId,
    Guid TenantId,
    string Status,
    string PlaybackUrl,
    string? ThumbnailUrl,
    string? Duration,
    int? Width,
    int? Height,
    string? EngineUsed,
    string? TargetResourceArn);

public class SQSVideoPublishedConsumer : IConsumer<VideoPublishedQueueMessage>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IModuleBus _moduleBus;
    private readonly IClock _clock;
    private readonly ILogger<SQSVideoPublishedConsumer> _logger;

    public SQSVideoPublishedConsumer(
        IVideoRepository videoRepository,
        IUnitOfWork unitOfWork,
        IModuleBus moduleBus,
        IClock clock,
        ILogger<SQSVideoPublishedConsumer> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _moduleBus = moduleBus;
        _clock = clock;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedQueueMessage> context)
    {
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
    }
}

public class SQSVideoPublishedConsumerDefinition : ConsumerDefinition<SQSVideoPublishedConsumer>
{
    public SQSVideoPublishedConsumerDefinition()
    {
        EndpointName = "VideoPublishedQueue";
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
