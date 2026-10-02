using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Commands.Delete;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Infrastructure consumer that acts as an entry point for S3-related events.
/// Business logic is delegated to the Application layer via the Module Bridge.
/// </summary>
public class VideoS3SyncHandlers : 
    IConsumer<VideoDeletedFromS3Event>,
    IConsumer<VideoMetadataUpdatedEvent>
{
    private readonly IVideoUploadingModule _module;
    private readonly ILogger<VideoS3SyncHandlers> _logger;

    public VideoS3SyncHandlers(IVideoUploadingModule module, ILogger<VideoS3SyncHandlers> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoDeletedFromS3Event> context)
    {
        _logger.LogInformation("Infrastructure: Received S3 deletion for key {Key}", context.Message.Key);
        
        var result = await _module.Send<MarkVideoAsDeletedCommand, ErrorOr<Success>>(
            new MarkVideoAsDeletedCommand(context.Message.Key), context.CancellationToken);
        
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