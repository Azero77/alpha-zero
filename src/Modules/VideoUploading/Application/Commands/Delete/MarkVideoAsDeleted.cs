using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Delete;

public record MarkVideoAsDeletedCommand(string SourceKey) : IRequest<ErrorOr<Success>>;

public sealed class MarkVideoAsDeletedCommandHandler : IRequestHandler<MarkVideoAsDeletedCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkVideoAsDeletedCommandHandler> _logger;

    public MarkVideoAsDeletedCommandHandler(
        IVideoRepository videoRepository, 
        IUnitOfWork unitOfWork, 
        ILogger<MarkVideoAsDeletedCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(MarkVideoAsDeletedCommand request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetBySourceKeyAsync(request.SourceKey, cancellationToken);
        if (video == null)
        {
            _logger.LogWarning("Video not found for SourceKey: {SourceKey}. Skipping deletion mark.", request.SourceKey);
            return Result.Success; // Or NotFound if we want to be strict
        }

        _logger.LogInformation("Marking video {VideoId} as deleted in database.", video.Id);
        video.MarkAsDeleted();
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success;
    }
}