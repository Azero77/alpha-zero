using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.MarkVideoAsOptimized;

public record MarkVideoAsOptimizedCommand(Guid VideoId, string S3OutputPrefix) : IRequest<ErrorOr<Success>>;

public sealed class MarkVideoAsOptimizedCommandHandler : IRequestHandler<MarkVideoAsOptimizedCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkVideoAsOptimizedCommandHandler> _logger;

    public MarkVideoAsOptimizedCommandHandler(
        IVideoRepository videoRepository, 
        IUnitOfWork unitOfWork, 
        ILogger<MarkVideoAsOptimizedCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(MarkVideoAsOptimizedCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking Video {VideoId} as optimized with output prefix {Prefix}", 
            request.VideoId, request.S3OutputPrefix);

        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        var result = video.MarkAsOptimized(request.S3OutputPrefix);
        if (result.IsError) return result.Errors;

        _videoRepository.Update(video);

        return Result.Success;
    }
}
