using AlphaZero.Modules.VideoUploading.Application.Commands.Delete;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Infrastructure consumer that acts as an entry point for S3-related events.
/// Business logic is delegated to the Application layer via Mediator.
/// </summary>
public class VideoS3SyncHandlers : 
    IConsumer<VideoDeletedFromS3Event>,
    IConsumer<VideoMetadataUpdatedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<VideoS3SyncHandlers> _logger;

    public VideoS3SyncHandlers(IMediator mediator, ILogger<VideoS3SyncHandlers> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoDeletedFromS3Event> context)
    {
        _logger.LogInformation("Infrastructure: Received S3 deletion for key {Key}", context.Message.Key);
        
        var result = await _mediator.Send(new MarkVideoAsDeletedCommand(context.Message.Key));
        
        if (result.IsError)
        {
            _logger.LogError("Failed to mark video as deleted: {Error}", result.FirstError.Description);
        }
    }

    public async Task Consume(ConsumeContext<VideoMetadataUpdatedEvent> context)
    {
        _logger.LogInformation("Infrastructure: Received metadata update for key {Key}", context.Message.Key);
        // Delegate to Application Command if we had one for metadata updates
        await Task.CompletedTask;
    }
}