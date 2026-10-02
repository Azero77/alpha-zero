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

        await _videoStateRepository.InitializeAsync(msg.VideoId, msg.TenantId, context.CancellationToken);
        
        _logger.LogInformation("Initialized VideoState for VideoId {VideoId}", msg.VideoId);
    }
}


