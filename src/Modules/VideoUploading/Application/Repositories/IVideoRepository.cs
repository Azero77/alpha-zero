using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.VideoUploading.Application.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Video?> GetBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default);
    Task<PagedResult<Video>> ListAsync(int page, int perPage, CancellationToken cancellationToken = default);
    Task AddAsync(Video video, CancellationToken cancellationToken = default);
    void Update(Video video);
    void Delete(Video video);
}