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
    private readonly IPublishEndpoint _moduleBus;
    private readonly IClock _clock;

    public CompleteVideoPublishingCommandHandler(
        IVideoRepository videoRepository,
        IVideoStateRepository videoStateRepository,
        IPublishEndpoint moduleBus,
        IClock clock)
    {
        _videoRepository = videoRepository;
        _videoStateRepository = videoStateRepository;
        _moduleBus = moduleBus;
        _clock = clock;
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
        return Result.Success;
    }
}

public class VideoPublishedDomainEventHandlerPublishIntegrationEvent(IPublishEndpoint moduleBus, IVideoRepository videoRepository) : INotificationHandler<VideoPublishedDomainEvent>
{
    public async Task Handle(VideoPublishedDomainEvent notification, CancellationToken cancellationToken)
    {   
        //first we delete the video state after finishing 
        var video = await videoRepository.GetById(notification.VideoId, cancellationToken);
        if (video is null) return;
        // Notify other modules
        await moduleBus.Publish(new VideoPublishedIntegrationEvent(
            notification.VideoId,
            video.PlaybackUrl!), cancellationToken);
    }
}

public class DeleteVideoStateVideoPublishedDomainEventHandler(IVideoStateRepository videoStateRepository)
    : INotificationHandler<VideoPublishedDomainEvent>
{
    public Task Handle(VideoPublishedDomainEvent notification, CancellationToken cancellationToken)
    {
        return videoStateRepository.RemoveAsync(notification.VideoId, cancellationToken);
    }
}
