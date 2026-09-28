using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;

using AlphaZero.Modules.VideoUploading.Application.Models;

namespace AlphaZero.Modules.VideoUploading.Application.Repositories;

public record VideoStateDto(
    Guid VideoId,
    Guid TenantId,
    PipelineStage Stage,
    string? MediaConverterJobId,
    int Version);

public interface IVideoStateRepository
{
    Task<VideoStateDto?> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid videoId, CancellationToken cancellationToken = default);
    Task InitializeAsync(Guid videoId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> TryUpdateStageAsync(Guid videoId, PipelineStage newStage, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid videoId, CancellationToken cancellationToken = default);
}