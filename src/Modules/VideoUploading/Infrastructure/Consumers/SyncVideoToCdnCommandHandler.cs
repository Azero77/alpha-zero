using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class SyncVideoToCdnCommandHandler : IConsumer<SyncVideoToCdnCommand>
{
    private readonly IVideoCdnSyncService _cdnSyncService;
    private readonly ILogger<SyncVideoToCdnCommandHandler> _logger;

    public SyncVideoToCdnCommandHandler(
        IVideoCdnSyncService cdnSyncService, 
        ILogger<SyncVideoToCdnCommandHandler> logger)
    {
        _cdnSyncService = cdnSyncService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SyncVideoToCdnCommand> context)
    {
        _logger.LogInformation("[Command] Syncing Video {VideoId} to CDN. Prefix: {Prefix}", 
            context.Message.VideoId, context.Message.S3KeyPrefix);

        var syncResult = await _cdnSyncService.SyncToCdnAsync(
            context.Message.VideoId, 
            context.Message.S3KeyPrefix, 
            context.Message.S3Bucket, 
            context.CancellationToken);

        if (syncResult.IsError)
        {
            _logger.LogError("Failed to sync video {VideoId} to CDN: {Error}", 
                context.Message.VideoId, syncResult.FirstError.Description);
            
            await context.Publish(new VideoProcessingFailedEvent(
                context.Message.VideoId, 
                $"CDN Sync failed: {syncResult.FirstError.Description}", 
                context.Message.S3KeyPrefix));
            return;
        }

        _logger.LogInformation("CDN Sync complete for Video {VideoId}. Public URL: {Url}", 
            context.Message.VideoId, syncResult.Value);

        await context.Publish(new VideoCdnSyncCompletedEvent(context.Message.VideoId, syncResult.Value));
    }
}
