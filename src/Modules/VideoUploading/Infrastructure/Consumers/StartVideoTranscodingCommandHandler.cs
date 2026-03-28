using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Infrastructure consumer that acts as an entry point for the StartVideoProcessingCommand.
/// Orchestration logic is handled by the Application layer via the Module Bridge.
/// </summary>
public class StartVideoProcessingCommandHandler : IConsumer<StartVideoProcessingCommand>
{
    private readonly IVideoUploadingModule _module;
    private readonly ILogger<StartVideoProcessingCommandHandler> _logger;

    public StartVideoProcessingCommandHandler(
        IVideoUploadingModule module,
        ILogger<StartVideoProcessingCommandHandler> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StartVideoProcessingCommand> context)
    {
        _logger.LogInformation("Infrastructure: Received StartVideoProcessingCommand for Video {VideoId}", context.Message.VideoId);
        
        var result = await _module.Send<Application.Commands.Process.StartVideoTranscodingCommand, ErrorOr<string>>(
            new Application.Commands.Process.StartVideoTranscodingCommand(
                context.Message.VideoId, 
                context.Message.Key, 
                context.Message.BucketName), context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Failed to start transcoding in application: {Error}", result.FirstError.Description);
            // MassTransit will handle retries if we throw, or we can handle it here
            throw new Exception($"Transcoding job creation failed: {result.FirstError.Description}");
        }
    }
}