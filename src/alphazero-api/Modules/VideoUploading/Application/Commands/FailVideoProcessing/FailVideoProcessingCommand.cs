using AlphaZero.Modules.VideoUploading.Application.Models;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Events;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MassTransit;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.FailVideoProcessing;

public record FailVideoProcessingCommand(Guid VideoId, string Reason) : ICommand<Success>;

public class FailVideoProcessingCommandHandler : IRequestHandler<FailVideoProcessingCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IVideoProgressNotifier _progressNotifier;

    public FailVideoProcessingCommandHandler(
        IVideoRepository videoRepository,
        IVideoProgressNotifier progressNotifier)
    {
        _videoRepository = videoRepository;
        _progressNotifier = progressNotifier;
    }

    public async Task<ErrorOr<Success>> Handle(FailVideoProcessingCommand request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video is null)
            return Error.NotFound("Video.NotFound", "Video not found");

        if (video.Status != VideoStatus.Failed)
        {
            video.MarkAsFailed(request.Reason);
        }
        return Result.Success;
    }
}

public class VideoFailedDomainEventHandlerPublishIntegrationEvent(IModuleBus moduleBus) : INotificationHandler<VideoFailedDomainEvent>
{
    public async Task Handle(VideoFailedDomainEvent notification, CancellationToken cancellationToken)
    {
        await moduleBus.Publish(new VideoProcessingFailedEvent(
            notification.VideoId,
            notification.Reason), cancellationToken);
    }
}

public class DeleteVideoStateVideoFailedDomainEventHandler(IVideoStateRepository videoStateRepository) : INotificationHandler<VideoFailedDomainEvent>
{
    public Task Handle(VideoFailedDomainEvent notification, CancellationToken cancellationToken)
    {
        return videoStateRepository.RemoveAsync(notification.VideoId, cancellationToken);
    }
}

public class NotifySignalRVideoFailedDomainEventHandler(IVideoProgressNotifier notifier)
    : INotificationHandler<VideoFailedDomainEvent>
{
    public Task Handle(VideoFailedDomainEvent notification, CancellationToken cancellationToken)
        => notifier.NotifyProgressAsync(new VideoProgressNotification(
            notification.VideoId.ToString(),
            notification.TenantId.ToString(),
            PipelineStage.Failed,
            "FAILED",
            notification.Reason
        ), cancellationToken);
}
