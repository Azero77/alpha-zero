using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
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
            .FirstOrDefaultAsync(s => s.CorrelationId == videoId, cancellationToken);

        if (state == null) return null;

        return new VideoStateDto(
            state.CorrelationId,
            state.TenantId,
            state.CurrentState,
            state.MediaConverterJobId,
            state.Key,
            state.ProcessingStarted,
            state.IsFailed,
            state.Version);
    }
}