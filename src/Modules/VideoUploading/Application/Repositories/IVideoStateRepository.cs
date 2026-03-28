using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Repositories;

public record VideoStateDto(
    Guid CorrelationId,
    Guid TenantId,
    string CurrentState,
    string? MediaConverterJobId,
    string? Key,
    bool ProcessingStarted,
    bool IsFailed,
    int Version);

public interface IVideoStateRepository
{
    Task<VideoStateDto?> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
}