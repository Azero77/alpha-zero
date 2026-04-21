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
        if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        Directory.CreateDirectory(tempPath);

        string sourceFile = Path.Combine(tempPath, "source.mp4");
        string outputDir = Path.Combine(tempPath, "output");
        Directory.CreateDirectory(outputDir);

        using var process = new Process();
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
                    
                    byte[] keyBytes = Convert.FromHexString(encParams.KeyValue);
                    await File.WriteAllBytesAsync(keyFilePath, keyBytes);

                    hlsKeyInfoFile = Path.Combine(tempPath, "key_info");
                    await File.WriteAllLinesAsync(hlsKeyInfoFile, new[]
                    {
                        encParams.KeyUrl ?? "video.key", 
                        keyFilePath,                     
                        encParams.KeyId                  
                    });
                    
                    _logger.LogInformation("[FFmpeg] Encryption enabled with method: {Method}", encMethod);
                }
            }

            // 3. Execute FFmpeg
            string ffmpegArgs = BuildFFmpegArgs(sourceFile, outputDir, msg.SourceWidth, hlsKeyInfoFile);
            _logger.LogInformation("[FFmpeg] Running command: ffmpeg {Args}", ffmpegArgs);
            
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var errorReaderTask = process.StandardError.ReadToEndAsync();

            try 
            {
                await process.WaitForExitAsync(context.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[FFmpeg] Transcoding cancelled for Video {VideoId}. Killing process.", msg.VideoId);
                if (!process.HasExited) process.Kill(true);
                throw;
            }

            if (process.ExitCode != 0)
            {
                string errorOutput = await errorReaderTask;
                _logger.LogError("[FFmpeg] Process failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, errorOutput);
                await context.Publish(new VideoProcessingFailedEvent(msg.VideoId, "FFmpeg processing failed", msg.SourceKey));
                return;
            }

            // 4. Upload to S3
            _logger.LogInformation("[FFmpeg] Uploading results to S3: {Prefix}", msg.DestinationPrefix);
            var transferUtility = new TransferUtility(_s3Client);
            await transferUtility.UploadDirectoryAsync(outputDir, _awsResources.OutputS3!.BucketName, msg.DestinationPrefix,SearchOption.AllDirectories, cancellationToken: context.CancellationToken);

            // 5. Notify Finish
            await context.Publish(new VideoTranscodingFinishedEvent(msg.VideoId, msg.DestinationPrefix));
            _logger.LogInformation("[FFmpeg] Transcoding completed successfully for Video {VideoId}", msg.VideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FFmpeg] Unexpected error during transcoding for Video {VideoId}", msg.VideoId);
            if (!process.HasExited) try { process.Kill(true); } catch { }
            await context.Publish(new VideoProcessingFailedEvent(msg.VideoId, ex.Message, msg.SourceKey));
        }
        finally
        {
            if (!process.HasExited) try { process.Kill(true); } catch { }
            try { Directory.Delete(tempPath, true); } catch { }
        }
    }

    private string BuildFFmpegArgs(string inputFile, string outputDir, int sourceWidth, string? keyInfoFile)
    {
        var renditions = new List<(int width, int height, int bitrate, string name)>
        {
            (1920, 1080, 3000, "1080p"),
            (1280, 720, 1800, "720p"),
            (854, 480, 800, "480p"),
            (640, 360, 500, "360p")
        };

        var activeRenditions = renditions.Where(r => r.width <= sourceWidth).ToList();
        if (activeRenditions.Count == 0) activeRenditions.Add(renditions.Last());

        var args = $"-i \"{inputFile}\" ";
        string varStreamMap = "";
        
        for (int i = 0; i < activeRenditions.Count; i++)
        {
            var r = activeRenditions[i];
            // Use CRF 21 for High Quality, 23 for Standard (mimics QVBR 7/8)
            int crf = r.width >= 1280 ? 21 : 23;
            
            args += $"-map 0:v -map 0:a ";
            // Bug Fix 3: QVBR equivalent using CRF + maxrate/bufsize
            args += $"-c:v:{i} libx264 -crf:{i} {crf} -maxrate:v:{i} {r.bitrate}k -bufsize:v:{i} {r.bitrate * 2}k -preset fast ";
            // Bug Fix 1: Noise Reduction (hqdn3d)
            args += $"-filter:v:{i} \"hqdn3d,scale=w={r.width}:h={r.height}:force_original_aspect_ratio=decrease,pad=ceil(iw/2)*2:ceil(ih/2)*2\" ";
            // Bug Fix 2: Audio Normalization (loudnorm)
            args += $"-c:a:{i} aac -b:a:{i} 128k -ar:{i} 44100 -filter:a:{i} \"loudnorm=I=-24:LRA=7:tp=-2\" ";
            varStreamMap += $"v:{i},a:{i} ";
        }

        args += $"-f hls -hls_time 6 -hls_playlist_type vod -hls_segment_type fmp4 -master_pl_name master.m3u8 ";
        // CMAF Specifics: Independent segments and I-Frame index for Trick Play (scrubbing)
        args += $"-hls_flags independent_segments -hls_fmp4_iframe_index 1 ";
        
        if (!string.IsNullOrEmpty(keyInfoFile))
        {
            args += $"-hls_key_info_file \"{keyInfoFile}\" ";
        }

        args += $"-var_stream_map \"{varStreamMap.Trim()}\" \"{outputDir}/stream_%v.m3u8\"";

        return args;
    }
}
