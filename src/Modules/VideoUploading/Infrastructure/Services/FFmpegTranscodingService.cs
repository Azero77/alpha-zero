using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;
using AlphaZero.Shared.Application;
using ErrorOr;
using MassTransit;
using MassTransit.Contracts.JobService;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class FFmpegTranscodingService : IVideoTranscodingService
{   
    private readonly ILogger<FFmpegTranscodingService> _logger;
    private readonly IModuleBus _moduleBus;

    public FFmpegTranscodingService(
        ILogger<FFmpegTranscodingService> logger, IModuleBus moduleBus)
    {
        _logger = logger;
        _moduleBus = moduleBus;
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


        // inputS3Uri is s3://bucket/key
        // outputPathS3Uri is s3://bucket/streaming/videoId/master.m3u8

        // Extract the key from inputS3Uri
        string sourceKey = S3Uri.Parse(inputS3Uri).Key;

        // Extract the prefix from outputPathS3Uri (streaming/videoId/)
        string destinationPrefix = S3Uri.Parse(outputPathS3Uri).Prefix;
        ExecuteFFmpegTranscodingCommand command = new(
            videoId,
            sourceKey,
            destinationPrefix,
            sourceWidth,
            sourceHeight,
            encryptionMethod.ToString());
        
        await _moduleBus.Publish<SubmitJob<ExecuteFFmpegTranscodingCommand>>(new { JobId = videoId , Job = command });
        _logger.LogInformation("[FFmpegService] Job {JobId} successfully saved to database.", videoId);
        return videoId.ToString(); //the video id is the job id, so we can track the job status using the video id
    }
}
