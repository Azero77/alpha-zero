using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.VideoUploading.Application.Repositories;

public interface IVideoRepository : IRepository<Video>
{
    Task<Video?> GetBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default);
    Task<PagedResult<Video>> ListAsync(int page, int perPage, CancellationToken cancellationToken = default);
    Task AddAsync(Video video, CancellationToken cancellationToken = default);
}