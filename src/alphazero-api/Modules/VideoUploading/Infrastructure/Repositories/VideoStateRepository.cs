using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Repositories;

public class VideoStateRepository : IVideoStateRepository
{
    private readonly AppDbContext _context;

    public VideoStateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<VideoStateDto?> GetByVideoIdAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var state = await _context.VideoState
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.VideoId == videoId, cancellationToken);

        if (state == null) return null;

        return new VideoStateDto(
            state.VideoId,
            state.TenantId,
            state.Stage,
            state.MediaConverterJobId,
            state.Key,
            state.CustomThumbnailKey,
            state.IsFailed,
            state.Version);
    }

    public async Task<bool> ExistsAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        return await _context.VideoState.AnyAsync(s => s.VideoId == videoId, cancellationToken);
    }

    public async Task InitializeAsync(Guid videoId, Guid tenantId, string? customThumbnailKey, string? targetResourceArn, CancellationToken cancellationToken = default)
    {
        var state = new AlphaZero.Modules.VideoUploading.Infrastructure.Sagas.VideoState
        {
            VideoId = videoId,
            TenantId = tenantId,
            Stage = AlphaZero.Modules.VideoUploading.Application.Models.PipelineStage.Uploaded,
            CustomThumbnailKey = customThumbnailKey,
            TargetResourceArn = targetResourceArn
        };
        _context.VideoState.Add(state);
    }

    public async Task<bool> TryUpdateStageAsync(Guid videoId, AlphaZero.Modules.VideoUploading.Application.Models.PipelineStage incomingStage, CancellationToken cancellationToken = default)
    {
        var state = await _context.VideoState.FirstOrDefaultAsync(s => s.VideoId == videoId, cancellationToken);
        if (state == null || (int)incomingStage <= (int)state.Stage)
        {
            return false; // Stale or duplicate message
        }
        
        state.Stage = incomingStage;
        return true;
    }

    public async Task RemoveAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var state = await _context.VideoState.FirstOrDefaultAsync(s => s.VideoId == videoId, cancellationToken);
        if (state != null)
        {
            _context.VideoState.Remove(state);
        }
    }
}
public class VideoSecretRepository : BaseRepository<AppDbContext,VideoSecret>,IRepository<VideoSecret>
{
    public VideoSecretRepository(AppDbContext context) : base(context)
    {
    }
}