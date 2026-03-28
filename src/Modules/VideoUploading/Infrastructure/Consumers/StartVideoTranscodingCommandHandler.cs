using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Infrastructure consumer that acts as an entry point for the StartVideoProcessingCommand.
/// Orchestration logic is handled by the Application layer.
/// </summary>
public class StartVideoProcessingCommandHandler : IConsumer<StartVideoProcessingCommand>
{
    private readonly IMediator _mediator;
    private readonly ILogger<StartVideoProcessingCommandHandler> _logger;

    public StartVideoProcessingCommandHandler(
        IMediator mediator,
        ILogger<StartVideoProcessingCommandHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StartVideoProcessingCommand> context)
    {
        _logger.LogInformation("Infrastructure: Received StartVideoProcessingCommand for Video {VideoId}", context.Message.VideoId);
        
        var result = await _mediator.Send(new Application.Commands.Process.StartVideoTranscodingCommand(
            context.Message.VideoId, 
            context.Message.Key, 
            context.Message.BucketName));

        if (result.IsError)
        {
            _logger.LogError("Failed to start transcoding in application: {Error}", result.FirstError.Description);
            // MassTransit will handle retries if we throw, or we can handle it here
            throw new Exception($"Transcoding job creation failed: {result.FirstError.Description}");
        }
    }
}