using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;
using AlphaZero.Shared.Application;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class FFmpegTranscodingService : IVideoTranscodingService
{
    private readonly IModuleBus _moduleBus;
    private readonly ILogger<FFmpegTranscodingService> _logger;

    public FFmpegTranscodingService(
        IModuleBus moduleBus,
        ILogger<FFmpegTranscodingService> logger)
    {
        _moduleBus = moduleBus;
        _logger = logger;
    }

    public VideoTranscodingMetehod Method => VideoTranscodingMetehod.FFMPEG;

    public async Task<ErrorOr<string>> StartTranscodingJobAsync(
        Guid videoId, 
        string inputS3Uri, 
        string outputPathS3Uri, 
        int sourceWidth,
        int sourceHeight,
        VideoEncryptionMethod encryptionMethod = VideoEncryptionMethod.None,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[FFmpegService] Initiating background transcoding for Video: {VideoId}", videoId);

        // We use the videoId as the JobId for FFmpeg tasks
        string jobId = videoId.ToString();

        // inputS3Uri is s3://bucket/key
        // outputPathS3Uri is s3://bucket/streaming/videoId/master.m3u8

        // Extract the key from inputS3Uri
        string sourceKey = S3Uri.Parse(inputS3Uri).Key;

        // Extract the prefix from outputPathS3Uri (streaming/videoId/)
        string destinationPrefix = S3Uri.Parse(outputPathS3Uri).Prefix;
        await _moduleBus.Publish(new ExecuteFFmpegTranscodingCommand(
            videoId,
            sourceKey,
            destinationPrefix,
            sourceWidth,
            sourceHeight,
            encryptionMethod.ToString()), cancellationToken);

        return jobId;
    }
}
