using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.MarkVideoAsLive;

public record MarkVideoAsLiveCommand(Guid VideoId, string FinalUrl) : IRequest<ErrorOr<Success>>;

public sealed class MarkVideoAsLiveCommandHandler : IRequestHandler<MarkVideoAsLiveCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<MarkVideoAsLiveCommandHandler> _logger;

    public MarkVideoAsLiveCommandHandler(
        IVideoRepository videoRepository, 
        IUnitOfWork unitOfWork, 
        IClock clock,
        ILogger<MarkVideoAsLiveCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(MarkVideoAsLiveCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking Video {VideoId} as LIVE with URL {Url}", 
            request.VideoId, request.FinalUrl);

        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        var result = video.MarkAsLive(request.FinalUrl, _clock);
        if (result.IsError) return result.Errors;

        _videoRepository.Update(video);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
