using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using Amazon.S3;
using Amazon.S3.Transfer;
using Aspire.Shared;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

public class FFmpegTranscodingConsumer : IConsumer<ExecuteFFmpegTranscodingCommand>
{
    private readonly IAmazonS3 _s3Client;
    private readonly AWSResources _awsResources;
    private readonly IVideoEncryptionService _encryptionService;
    private readonly ILogger<FFmpegTranscodingConsumer> _logger;

    public FFmpegTranscodingConsumer(
        IAmazonS3 s3Client,
        AWSResources awsResources,
        IVideoEncryptionService encryptionService,
        ILogger<FFmpegTranscodingConsumer> logger)
    {
        _s3Client = s3Client;
        _awsResources = awsResources;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExecuteFFmpegTranscodingCommand> context)
    {
        var msg = context.Message;
        _logger.LogInformation("[FFmpegConsumer] Starting transcoding for Video {VideoId}", msg.VideoId);

        string tempPath = Path.Combine(Path.GetTempPath(), "transcoding", msg.VideoId.ToString());
        Directory.CreateDirectory(tempPath);

        string sourceFile = Path.Combine(tempPath, "source.mp4");
        string outputDir = Path.Combine(tempPath, "output");
        Directory.CreateDirectory(outputDir);

        try
        {
            // 1. Download source from S3
            _logger.LogInformation("[FFmpeg] Downloading source from S3: {Key}", msg.SourceKey);
            await _s3Client.DownloadToFilePathAsync(
                _awsResources.InputS3!.BucketName, 
                msg.SourceKey, 
                sourceFile, 
                null, 
                context.CancellationToken);

            // 2. Encryption Setup
            string? hlsKeyInfoFile = null;
            if (Enum.TryParse<VideoEncryptionMethod>(msg.EncryptionMethod, out var encMethod) && encMethod != VideoEncryptionMethod.None)
            {
                var encParamsResult = await _encryptionService.GetEncryptionParamsAsync(msg.VideoId, encMethod, context.CancellationToken);
                if (!encParamsResult.IsError)
                {
                    var encParams = encParamsResult.Value;
                    string keyFilePath = Path.Combine(tempPath, "video.key");
                    
                    // keyValue is hex string, convert to bytes
                    byte[] keyBytes = Convert.FromHexString(encParams.KeyValue);
                    await File.WriteAllBytesAsync(keyFilePath, keyBytes);

                    hlsKeyInfoFile = Path.Combine(tempPath, "key_info");
                    await File.WriteAllLinesAsync(hlsKeyInfoFile, new[]
                    {
                        encParams.KeyUrl ?? "video.key", // Key URL
                        keyFilePath,                     // Path to key file for FFmpeg
                        encParams.KeyId                  // Key ID (optional in some versions but good practice)
                    });
                    
                    _logger.LogInformation("[FFmpeg] Encryption enabled with method: {Method}", encMethod);
                }
            }

            // 3. Execute FFmpeg
            // We'll build a command for ABR HLS
            // This is a simplified version of what MediaConvert does
            string ffmpegArgs = BuildFFmpegArgs(sourceFile, outputDir, msg.SourceWidth, hlsKeyInfoFile);
            
            _logger.LogInformation("[FFmpeg] Running command: ffmpeg {Args}", ffmpegArgs);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) throw new Exception("Failed to start FFmpeg process");

            string errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(context.CancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("[FFmpeg] Process failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, errorOutput);
                await context.Publish(new VideoProcessingFailedEvent(msg.VideoId, "FFmpeg processing failed", msg.SourceKey));
                return;
            }

            // 4. Upload to S3
            _logger.LogInformation("[FFmpeg] Uploading results to S3: {Prefix}", msg.DestinationPrefix);
            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadDirectoryAsync(outputDir, _awsResources.OutputS3!.BucketName, msg.DestinationPrefix, context.CancellationToken);

            // 5. Notify Finish
            await context.Publish(new VideoTranscodingFinishedEvent(msg.VideoId, msg.DestinationPrefix));
            _logger.LogInformation("[FFmpeg] Transcoding completed successfully for Video {VideoId}", msg.VideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FFmpeg] Unexpected error during transcoding for Video {VideoId}", msg.VideoId);
            await context.Publish(new VideoProcessingFailedEvent(msg.VideoId, ex.Message, msg.SourceKey));
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(tempPath, true); } catch { }
        }
    }

    private string BuildFFmpegArgs(string inputFile, string outputDir, int sourceWidth, string? keyInfoFile)
    {
        // Define renditions based on source width
        var renditions = new List<(int width, int height, int bitrate, string name)>
        {
            (1920, 1080, 3000, "1080p"),
            (1280, 720, 1800, "720p"),
            (854, 480, 800, "480p"),
            (640, 360, 500, "360p")
        };

        // Filter out upscaling
        var activeRenditions = renditions.Where(r => r.width <= sourceWidth).ToList();
        if (activeRenditions.Count == 0) activeRenditions.Add(renditions.Last()); // At least keep 360p

        var args = $"-i \"{inputFile}\" ";
        string varStreamMap = "";
        
        for (int i = 0; i < activeRenditions.Count; i++)
        {
            var r = activeRenditions[i];
            args += $"-map 0:v -map 0:a ";
            args += $"-c:v:{i} h264 -b:v:{i} {r.bitrate}k -maxrate:v:{i} {r.bitrate}k -bufsize:v:{i} {r.bitrate * 2}k ";
            args += $"-filter:v:{i} \"scale=w={r.width}:h={r.height}:force_original_aspect_ratio=decrease,pad=ceil(iw/2)*2:ceil(ih/2)*2\" ";
            args += $"-c:a:{i} aac -b:a:{i} 64k -ar:{i} 44100 ";
            varStreamMap += $"v:{i},a:{i} ";
        }

        args += $"-f hls -hls_time 6 -hls_playlist_type vod -hls_segment_type fmp4 -master_pl_name master.m3u8 ";
        
        if (!string.IsNullOrEmpty(keyInfoFile))
        {
            args += $"-hls_key_info_file \"{keyInfoFile}\" ";
        }

        args += $"-var_stream_map \"{varStreamMap.Trim()}\" \"{outputDir}/stream_%v.m3u8\"";

        return args;
    }
}
