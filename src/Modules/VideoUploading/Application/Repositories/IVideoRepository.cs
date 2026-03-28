using AlphaZero.Modules.VideoUploading.Domain.Models;

namespace AlphaZero.Modules.VideoUploading.Application.Repositories;

public interface IVideoRepository
{
    Task<Video?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Video?> GetBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<Video>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Video video, CancellationToken cancellationToken = default);
    void Update(Video video);
    void Delete(Video video);
}