using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Delete;

public record DeleteVideoCommand(Guid VideoId) : IRequest<ErrorOr<Deleted>>;

public sealed class DeleteVideoCommandHandler : IRequestHandler<DeleteVideoCommand, ErrorOr<Deleted>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteVideoCommandHandler> _logger;

    public DeleteVideoCommandHandler(
        IVideoRepository videoRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteVideoCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ErrorOr<Deleted>> Handle(DeleteVideoCommand request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        _logger.LogInformation("Deleting video {VideoId} from database.", video.Id);
        _videoRepository.Delete(video);

        return Result.Deleted;
    }
}