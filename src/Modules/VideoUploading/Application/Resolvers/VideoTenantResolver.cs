using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.VideoUploading.Application.Resolvers;

public class VideoTenantResolver(IVideoRepository videoRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Video;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var video = await videoRepository.GetById(resourceId);
        return video?.TenantId;
    }
}

public class VideosTenantResolver(IVideoRepository videoRepository) : IResourceTenantResolver
{
    public ResourceType ResourceType => ResourceType.Videos;

    public async Task<Guid?> ResolveTenantIdAsync(Guid resourceId, CancellationToken ct)
    {
        var video = await videoRepository.GetById(resourceId);
        return video?.TenantId;
    }
}
