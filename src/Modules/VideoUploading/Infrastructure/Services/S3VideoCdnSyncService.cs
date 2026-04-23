using System.Linq;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Shared;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class S3VideoCdnSyncService : IVideoCdnSyncService
{
    private readonly IAmazonS3 _s3Client;
    private readonly AWSResources _awsResources;
    private readonly ILogger<S3VideoCdnSyncService> _logger;
    private readonly IModuleBus _moduleBus;

    public S3VideoCdnSyncService(
        IAmazonS3 s3Client,
        AWSResources awsResources,
        ILogger<S3VideoCdnSyncService> logger,
        IModuleBus moduleBus)
    {
        _s3Client = s3Client;
        _awsResources = awsResources;
        _logger = logger;
        _moduleBus = moduleBus;
    }
    public async Task<ErrorOr<string>> SyncToCdnAsync(Guid videoId, string s3KeyPrefix, string s3Bucket, string? customThumbnailKey, CancellationToken cancellationToken = default)
    {
        var destinationBucket = _awsResources.CdnS3?.BucketName;
        if (string.IsNullOrEmpty(destinationBucket))
        {
            _logger.LogError("CDN S3 bucket is not configured.");
            return Error.Failure("CdnSync.MissingConfiguration", "CDN S3 bucket is not configured.");
        }

        try
        {
            _logger.LogInformation("Starting Parallel CDN Sync for Video {VideoId} from {SourceBucket}/{Prefix}", 
                videoId, s3Bucket, s3KeyPrefix);

            // 1. List all objects in the transcoding output
            var listRequest = new ListObjectsV2Request { BucketName = s3Bucket, Prefix = s3KeyPrefix };
            var objectsToSync = new List<S3Object>();
            ListObjectsV2Response listResponse;
            do
            {
                listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);
                objectsToSync.AddRange(listResponse.S3Objects);
                listRequest.ContinuationToken = listResponse.NextContinuationToken;
            } while (listResponse.IsTruncated ?? false);

            if (objectsToSync.Count == 0) return Error.NotFound("CdnSync.NoFiles");

            // 2. Parallel Copy
            var copyTasks = objectsToSync.Select(s3Object => 
                _s3Client.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = s3Bucket,
                    SourceKey = s3Object.Key,
                    DestinationBucket = destinationBucket,
                    DestinationKey = s3Object.Key
                }, cancellationToken)).ToList();

            // 3. Add Custom Thumbnail to Sync if exists
            if (!string.IsNullOrEmpty(customThumbnailKey))
            {
                _logger.LogInformation("Syncing custom thumbnail {Key} for Video {VideoId}", customThumbnailKey, videoId);
                copyTasks.Add(_s3Client.CopyObjectAsync(new CopyObjectRequest
                {
                    SourceBucket = s3Bucket, // Assumed to be in the same input bucket
                    SourceKey = customThumbnailKey,
                    DestinationBucket = destinationBucket,
                    DestinationKey = $"{s3KeyPrefix.TrimEnd('/')}/thumbnails/custom.jpg" // Renamed for consistency
                }, cancellationToken));
            }

            var copyResults = await Task.WhenAll(copyTasks);
            if (copyTasks.Any(t => t.IsFaulted))
            {
                _logger.LogCritical("Sync video to cdn faulted , please delete the prefix from the cdn s3");
                await _moduleBus.Publish(new VideoProcessingFailedEvent(videoId, "Sync Failed", s3KeyPrefix));
                return Error.Failure("CdnSync.Failure","One task has faulted");
                //here we should have a handler deletes all the files from the cdn s3 
            }
            _logger.LogInformation("Successfully copied {Count} files in parallel for Video {VideoId}", copyTasks.Count, videoId);

            // 4. Cleanup Source (Only streaming files, maybe keep custom thumbnail for safety or delete it too)
            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = s3Bucket,
                Objects = objectsToSync.Select(o => new KeyVersion { Key = o.Key }).ToList()
            };
            if (!string.IsNullOrEmpty(customThumbnailKey))
            {
                deleteRequest.Objects.Add(new KeyVersion { Key = customThumbnailKey });
            }

            await _s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);

            // 4. Return Relative Path (Frontend will append CDN Domain)
            return $"{s3KeyPrefix.TrimEnd('/')}/master.m3u8";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during parallel sync for video {VideoId}", videoId);
            return Error.Failure("CdnSync.Failure", ex.Message);
        }
    }
}
