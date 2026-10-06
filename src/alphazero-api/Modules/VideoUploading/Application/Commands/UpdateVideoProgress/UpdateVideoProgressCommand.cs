using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Application.Models;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.UpdateVideoProgress;

public record UpdateVideoProgressCommand(
    Guid VideoId,
    Guid TenantId,
    PipelineStage Stage,
    string Status,
    string? Metadata) : ICommand<Success>;

public class UpdateVideoProgressCommandHandler : IRequestHandler<UpdateVideoProgressCommand, ErrorOr<Success>>
{
    private readonly IVideoStateRepository _videoStateRepository;
    private readonly IVideoProgressNotifier _progressNotifier;

    public UpdateVideoProgressCommandHandler(
        IVideoStateRepository videoStateRepository,
        IVideoProgressNotifier progressNotifier)
    {
        _videoStateRepository = videoStateRepository;
        _progressNotifier = progressNotifier;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateVideoProgressCommand request, CancellationToken cancellationToken)
    {
        var updated = await _videoStateRepository.TryUpdateStageAsync(request.VideoId, request.Stage, cancellationToken);
        
        if (updated || !await _videoStateRepository.ExistsAsync(request.VideoId, cancellationToken))
        {
            // If updated or doesn't exist (fallback signalR), notify
            await _progressNotifier.NotifyProgressAsync(new VideoProgressNotification(
                request.VideoId.ToString(),
                request.TenantId.ToString(),
                request.Stage.ToString().ToLowerInvariant(),
                request.Status,
                request.Metadata
            ), cancellationToken);
        }

        return Result.Success;
    }
}
