using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using AlphaZero.Modules.VideoUploading.Application.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class InitializeVideoStateConsumer : IConsumer<UploadVideoRequestedEvent>
{
    private readonly IVideoStateRepository _videoStateRepository;
    private readonly ILogger<InitializeVideoStateConsumer> _logger;

    public InitializeVideoStateConsumer(IVideoStateRepository videoStateRepository, ILogger<InitializeVideoStateConsumer> logger)
    {
        _videoStateRepository = videoStateRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UploadVideoRequestedEvent> context)
    {
        var msg = context.Message;
        
        // Ensure idempotency
        var exists = await _videoStateRepository.ExistsAsync(msg.VideoId, context.CancellationToken);
        if (exists)
        {
            _logger.LogWarning("VideoState already exists for VideoId {VideoId}. Skipping.", msg.VideoId);
            return;
        }

        await _videoStateRepository.InitializeAsync(msg.VideoId, msg.TenantId, msg.ThumbnailKey, msg.TargetResourceArn, context.CancellationToken);
        
        _logger.LogInformation("Initialized VideoState for VideoId {VideoId}", msg.VideoId);
    }
}

public class CleanupVideoStateConsumer : IConsumer<VideoPublishedEvent>, IConsumer<VideoProcessingFailedEvent>
{
    private readonly IVideoStateRepository _videoStateRepository;
    private readonly ILogger<CleanupVideoStateConsumer> _logger;

    public CleanupVideoStateConsumer(IVideoStateRepository videoStateRepository, ILogger<CleanupVideoStateConsumer> logger)
    {
        _videoStateRepository = videoStateRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoPublishedEvent> context)
    {
        await CleanupAsync(context.Message.VideoId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<VideoProcessingFailedEvent> context)
    {
        // On failure, we might want to keep the state for debugging, or drop it.
        // The architecture says "delete on publish". Let's delete on fail too.
        await CleanupAsync(context.Message.VideoId, context.CancellationToken);
    }

    private async Task CleanupAsync(Guid videoId, CancellationToken cancellationToken)
    {
        await _videoStateRepository.RemoveAsync(videoId, cancellationToken);
        _logger.LogInformation("Cleaned up VideoState for VideoId {VideoId}", videoId);
    }
}
