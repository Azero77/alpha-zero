using AlphaZero.Modules.VideoUploading.Application.Queries;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Queries;

public class VideoQueryService : IVideoQueryService
{
    private readonly AppDbContext _context;

    public VideoQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetVideoSecretKeyAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<VideoSecret>()
            .AsNoTracking()
            .Where(s => s.VideoId == videoId)
            .Select(s => s.KeyValue)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
