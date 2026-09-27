using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;
using AlphaZero.Modules.VideoUploading.Application.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class InitializeVideoStateConsumer : IConsumer<UploadVideoRequestedEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<InitializeVideoStateConsumer> _logger;

    public InitializeVideoStateConsumer(AppDbContext context, ILogger<InitializeVideoStateConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UploadVideoRequestedEvent> context)
    {
        var msg = context.Message;
        
        // Ensure idempotency
        var exists = await _context.VideoState.AnyAsync(s => s.VideoId == msg.VideoId, context.CancellationToken);
        if (exists)
        {
            _logger.LogWarning("VideoState already exists for VideoId {VideoId}. Skipping.", msg.VideoId);
            return;
        }

        var state = new VideoState
        {
            VideoId = msg.VideoId,
            TenantId = msg.TenantId,
            Stage = PipelineStage.Uploaded,
            CustomThumbnailKey = msg.ThumbnailKey,
            TargetResourceArn = msg.TargetResourceArn
        };

        _context.VideoState.Add(state);
        await _context.SaveChangesAsync(context.CancellationToken);
        
        _logger.LogInformation("Initialized VideoState for VideoId {VideoId}", msg.VideoId);
    }
}

public class CleanupVideoStateConsumer : IConsumer<VideoPublishedEvent>, IConsumer<VideoProcessingFailedEvent>
{
    private readonly AppDbContext _context;
    private readonly ILogger<CleanupVideoStateConsumer> _logger;

    public CleanupVideoStateConsumer(AppDbContext context, ILogger<CleanupVideoStateConsumer> logger)
    {
        _context = context;
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
        var state = await _context.VideoState.FirstOrDefaultAsync(s => s.VideoId == videoId, cancellationToken);
        if (state != null)
        {
            _context.VideoState.Remove(state);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleaned up VideoState for VideoId {VideoId}", videoId);
        }
    }
}
