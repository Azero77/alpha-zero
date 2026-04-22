using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Services;

public interface IVideoCdnSyncService
{
    /// <summary>
    /// Synchronizes processed video content from a transient S3 location to the CDN storage (R2 or another S3 bucket).
    /// </summary>
    /// <param name="videoId">The unique ID of the video.</param>
    /// <param name="s3KeyPrefix">The prefix where the files are located in the source bucket.</param>
    /// <param name="s3Bucket">The source S3 bucket name.</param>
    /// <param name="customThumbnailKey">The key for the custom thumbnail uploaded by the user, if any.</param>
    /// <returns>The public URL where the video is accessible.</returns>
    Task<ErrorOr<string>> SyncToCdnAsync(Guid videoId, string s3KeyPrefix, string s3Bucket, string? customThumbnailKey = null, CancellationToken cancellationToken = default);
}
