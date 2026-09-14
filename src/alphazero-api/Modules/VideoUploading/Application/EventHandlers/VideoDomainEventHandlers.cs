using AlphaZero.Modules.VideoUploading.Domain.Events;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.EventHandlers;

public class VideoDomainEventHandlers :
    INotificationHandler<VideoMetadataUpdatedDomainEvent>
{
    private readonly IModuleBus _publishEndpoint;
    private readonly ILogger<VideoDomainEventHandlers> _logger;

    public VideoDomainEventHandlers(
        IModuleBus publishEndpoint,
        ILogger<VideoDomainEventHandlers> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(VideoMetadataUpdatedDomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new VideoMetadataChangedIntegrationEvent(
            notification.VideoId,
            notification.Title,
            notification.Description);

        await _publishEndpoint.Publish(integrationEvent, ct);
        _logger.LogInformation("Published MetadataChanged integration event for Video {Id}", notification.VideoId);
    }
}
