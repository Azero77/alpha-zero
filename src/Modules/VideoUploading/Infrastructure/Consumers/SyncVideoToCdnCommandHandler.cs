using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class SyncVideoToCdnCommandHandler : IConsumer<SyncVideoToCdnCommand>
{
    private readonly IVideoCdnSyncService _cdnSyncService;
    private readonly ILogger<SyncVideoToCdnCommandHandler> _logger;
    private readonly AWSResources _aWSResources;

    public SyncVideoToCdnCommandHandler(
        IVideoCdnSyncService cdnSyncService,
        ILogger<SyncVideoToCdnCommandHandler> logger,
        AWSResources aWSResources)
    {
        _cdnSyncService = cdnSyncService;
        _logger = logger;
        _aWSResources = aWSResources;
    }

    public async Task Consume(ConsumeContext<SyncVideoToCdnCommand> context)
    {
        _logger.LogInformation("[Command] Syncing Video {VideoId} to CDN. Prefix: {Prefix}", 
            context.Message.VideoId, context.Message.S3KeyPrefix);

        var syncResult = await _cdnSyncService.SyncToCdnAsync(
            context.Message.VideoId, 
            context.Message.S3KeyPrefix, 
            _aWSResources!.OutputS3!.BucketName, 
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

        _logger.LogInformation("CDN Sync complete for Video {VideoId}. Relative Path: {Path}", 
            context.Message.VideoId, syncResult.Value);

        await context.Publish(new VideoCdnSyncCompletedEvent(context.Message.VideoId, syncResult.Value));
    }
}
