using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.VideoUploading.Application.Queries.ListVideos;

public record ListVideosQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<Video>>>;

public sealed class ListVideosQueryHandler : IRequestHandler<ListVideosQuery, ErrorOr<PagedResult<Video>>>
{
    private readonly IVideoRepository _videoRepository;

    public ListVideosQueryHandler(IVideoRepository videoRepository)
    {
        _videoRepository = videoRepository;
    }

    public async Task<ErrorOr<PagedResult<Video>>> Handle(ListVideosQuery request, CancellationToken cancellationToken)
    {
        var result = await _videoRepository.ListAsync(request.Page, request.PerPage, cancellationToken);
        return result;
    }
}