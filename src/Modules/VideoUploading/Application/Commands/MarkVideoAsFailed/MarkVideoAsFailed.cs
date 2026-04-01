using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.MarkVideoAsFailed;

public record MarkVideoAsFailedCommand(Guid VideoId, string Reason) : ICommand<Success>;

public sealed class MarkVideoAsFailedCommandHandler : IRequestHandler<MarkVideoAsFailedCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkVideoAsFailedCommandHandler> _logger;

    public MarkVideoAsFailedCommandHandler(
        IVideoRepository videoRepository, 
        IUnitOfWork unitOfWork, 
        ILogger<MarkVideoAsFailedCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(MarkVideoAsFailedCommand request, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Marking Video {VideoId} as FAILED. Reason: {Reason}", 
            request.VideoId, request.Reason);

        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        video.MarkAsFailed();
        _videoRepository.Update(video);

        return Result.Success;
    }
}
