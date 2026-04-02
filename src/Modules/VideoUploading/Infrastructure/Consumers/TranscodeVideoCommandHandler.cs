using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Infrastructure consumer that acts as an entry point for the TranscodeVideoCommand.
/// Orchestration logic is handled by the Application layer via the Module Bridge.
/// </summary>
public class TranscodeVideoCommandHandler : IConsumer<TranscodeVideoCommand>
{
    private readonly IVideoUploadingModule _module;
    private readonly ILogger<TranscodeVideoCommandHandler> _logger;

    public TranscodeVideoCommandHandler(
        IVideoUploadingModule module,
        ILogger<TranscodeVideoCommandHandler> logger)
    {
        _module = module;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TranscodeVideoCommand> context)
    {
        _logger.LogInformation("Infrastructure: Received TranscodeVideoCommand for Video {VideoId}. Dimensions: {Width}x{Height}", 
            context.Message.VideoId, context.Message.Width, context.Message.Height);
        
        var result = await _module.Send<Application.Commands.Process.StartVideoTranscodingCommand, ErrorOr<string>>(
            new Application.Commands.Process.StartVideoTranscodingCommand(
                context.Message.VideoId, 
                context.Message.Key, 
                context.Message.Width,
                context.Message.Height), context.CancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Failed to start transcoding in application: {Error}", result.FirstError.Description);
            
            await context.Publish(new VideoProcessingFailedEvent(
                context.Message.VideoId, 
                $"Transcoding failed to start: {result.FirstError.Description}",
                context.Message.Key));
            return;
        }

        var jobId = result.Value;
        _logger.LogInformation("Transcoding job started for Video {VideoId}. JobId: {JobId}", 
            context.Message.VideoId, jobId);

        await context.Publish(new VideoTranscodingStartedEvent(context.Message.VideoId, jobId));
    }
}
