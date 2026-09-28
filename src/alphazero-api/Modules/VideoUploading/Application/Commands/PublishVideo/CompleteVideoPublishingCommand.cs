using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Modules.VideoUploading.Application.Models;
using AlphaZero.Modules.VideoUploading.Domain.Events;
using ErrorOr;
using MediatR;
using MassTransit;
using VideoMetadata = AlphaZero.Modules.VideoUploading.Domain.Models.VideoMetadata;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.PublishVideo;

public record CompleteVideoPublishingCommand(
    Guid VideoId,
    Guid TenantId,
    string PlaybackUrl,
    string? Duration,
    int? Width,
    int? Height,
    string? TargetResourceArn) :ICommand<Success>;

public class CompleteVideoPublishingCommandHandler : IRequestHandler<CompleteVideoPublishingCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IVideoStateRepository _videoStateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IModuleBus _moduleBus;
    private readonly IClock _clock;
    private readonly IVideoProgressNotifier _progressNotifier;

    public CompleteVideoPublishingCommandHandler(
        IVideoRepository videoRepository,
        IVideoStateRepository videoStateRepository,
        IUnitOfWork unitOfWork,
        IModuleBus moduleBus,
        IClock clock,
        IVideoProgressNotifier progressNotifier)
    {
        _videoRepository = videoRepository;
        _videoStateRepository = videoStateRepository;
        _unitOfWork = unitOfWork;
        _moduleBus = moduleBus;
        _clock = clock;
        _progressNotifier = progressNotifier;
    }

    public async Task<ErrorOr<Success>> Handle(CompleteVideoPublishingCommand request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video is null)
        {
            return Error.NotFound("Video.NotFound", $"Video {request.VideoId} not found");
        }

        if (video.Status == VideoStatus.Published)
        {
            return Result.Success; // Idempotency check
        }

        TimeSpan duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(request.Duration) && TimeSpan.TryParse(request.Duration, out var parsedDuration))
        {
            duration = parsedDuration;
        }

        var resolution = request is { Width: not null, Height: not null }
            ? new Resolution(request.Width.Value, request.Height.Value)
            : Resolution.Empty;

        video.UpdateSpecifications(new VideoSpecifications(duration, resolution));
        video.MarkAsPublished( _clock.Now);

        // Remove transient state
        await _videoStateRepository.RemoveAsync(request.VideoId, cancellationToken);


        // Notify other modules
        await _moduleBus.Publish(new VideoPublishedEvent(
            video.Id,
            request.PlaybackUrl,
            request.TargetResourceArn), cancellationToken);

        // Notify SignalR clients
        

        return Result.Success;
    }
}

public class NotifyVideoPublishedHandler(IVideoProgressNotifier progressNotifier) : INotificationHandler<VideoPublishedDomainEvent>
{
    public async Task Handle(VideoPublishedDomainEvent notification, CancellationToken cancellationToken)
    {
        await progressNotifier.NotifyProgressAsync(new VideoProgressNotification(
            notification.VideoId.ToString(),
            notification.TenantId.ToString(),
            PipelineStage.Published,
            "COMPLETE",
            null
        ), cancellationToken);
    }
}
