using AlphaZero.Modules.VideoUploading.Application.Repositories;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoState;

public record GetVideoStateQuery(Guid VideoId) : IRequest<ErrorOr<VideoStateDto>>;

public sealed class GetVideoStateQueryHandler : IRequestHandler<GetVideoStateQuery, ErrorOr<VideoStateDto>>
{
    private readonly IVideoStateRepository _videoStateRepository;

    public GetVideoStateQueryHandler(IVideoStateRepository videoStateRepository)
    {
        _videoStateRepository = videoStateRepository;
    }

    public async Task<ErrorOr<VideoStateDto>> Handle(GetVideoStateQuery request, CancellationToken cancellationToken)
    {
        var state = await _videoStateRepository.GetByVideoIdAsync(request.VideoId, cancellationToken);
        if (state == null)
        {
            return Error.NotFound("VideoState.NotFound", $"Video state for ID {request.VideoId} was not found.");
        }

        return state;
    }
}