using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Application.Resolvers;

public class VideoTenantResolver(AppDbContext dbContext) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Video;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        // Try both Video and Videos enum names if they exist, 
        // but here we focus on the Video resource.
        return await dbContext.Set<AlphaZero.Modules.VideoUploading.Domain.Video>()
            .Where(v => v.Id == resourceId)
            .Select(v => (Guid?)v.TenantId)
            .FirstOrDefaultAsync(ct);
    }
}
