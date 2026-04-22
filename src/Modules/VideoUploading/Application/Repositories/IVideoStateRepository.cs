using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Repositories;

public record VideoStateDto(
    Guid CorrelationId,
    Guid TenantId,
    string CurrentState,
    string? MediaConverterJobId,
    string? Key,
    string? CustomThumbnailKey,
    bool IsFailed,
    int Version);

public interface IVideoStateRepository
{
    Task<VideoStateDto?> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
}