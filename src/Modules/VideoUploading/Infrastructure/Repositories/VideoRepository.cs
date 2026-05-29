using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Repositories;

public class VideoRepository : BaseRepository<AppDbContext, Video>, IVideoRepository
{
    public VideoRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Video?> GetBySourceKeyAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return await _context.Videos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.SourceKey == sourceKey, cancellationToken);
    }

    public async Task<PagedResult<Video>> ListAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Videos.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(v => v.CreatedOn)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);
            
        return new PagedResult<Video>(items, totalCount, page, perPage);
    }

    public async Task AddAsync(Video video, CancellationToken cancellationToken = default)
    {
        await _context.Videos.AddAsync(video, cancellationToken);
    }
}