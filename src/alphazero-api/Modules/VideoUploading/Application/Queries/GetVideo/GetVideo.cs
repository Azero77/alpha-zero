using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.GetVideo;

public record GetVideoQuery(Guid VideoId) : IRequest<ErrorOr<Video>>;

public sealed class GetVideoQueryHandler : IRequestHandler<GetVideoQuery, ErrorOr<Video>>
{
    private readonly IVideoRepository _videoRepository;

    public GetVideoQueryHandler(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task<ErrorOr<Video>> Handle(GetVideoQuery request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        return video;
    }
}