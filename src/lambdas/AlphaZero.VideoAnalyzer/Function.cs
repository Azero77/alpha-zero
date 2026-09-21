using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using AlphaZero.VideoPipeline.Exceptions;
using FFMpegCore;
using FFMpegCore.Arguments;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AlphaZero.VideoAnalyzer.Tests")]

namespace AlphaZero.VideoAnalyzer;

public record VideoAnalyzerInput(
    string VideoId,
    string TenantId,
    string SourceBucket,
    string SourceKey,
    string? TargetResourceArn = null,
    string? TranscodingEngine = "FFMPEG",
    string? EncryptionMethod = "ClearKey");

public record VideoAnalyzerOutput(
    int SourceWidth,
    int SourceHeight,
    double DurationSeconds,
    string DurationFormatted,
    double FrameRate,
    string AspectRatio,
    string VideoCodec,
    string AudioCodec,
    int AudioBitrateKbps,
    int AudioSampleRate);

public class Function
{
    private readonly IAmazonS3 _s3Client;

    public Function() : this(new AmazonS3Client()) { }
    public Function(IAmazonS3 s3Client) => _s3Client = s3Client;

    public async Task<VideoAnalyzerOutput> FunctionHandler(VideoAnalyzerInput input, ILambdaContext context)
    {
        context.Logger.LogInformation($"[Analyzer] Inspecting video {input.VideoId} in {input.SourceBucket}/{input.SourceKey}");

        try
        {
            var presignedUrl = await _s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = input.SourceBucket,
                Key = input.SourceKey,
                Expires = DateTime.UtcNow.AddMinutes(15),
                Verb = HttpVerb.GET
            });

            var mediaInfo = await FFProbe.AnalyseAsync(new Uri(presignedUrl));
            return MapMediaInfoToOutput(mediaInfo);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new VideoProcessingException($"Source file not found: {input.SourceKey}", ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new TransientException($"S3 interaction failed: {ex.Message}", ex);
        }
    }
    internal static VideoAnalyzerOutput MapMediaInfoToOutput(IMediaAnalysis mediaInfo)
        {
            var videoStream = mediaInfo.PrimaryVideoStream 
                ?? throw new VideoProcessingException("File has no main video stream.");
  
            var audioStream = mediaInfo.PrimaryAudioStream;
            var duration = mediaInfo.Duration;
            var durationFormatted = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
  
            string aspectRatio;
            if (videoStream.DisplayAspectRatio.Width > 0 && videoStream.DisplayAspectRatio.Height > 0)
            {
                aspectRatio = $"{videoStream.DisplayAspectRatio.Width}:{videoStream.DisplayAspectRatio.Height}";
            }
            else
            {
                var ratio = (double)videoStream.Width / videoStream.Height;
                aspectRatio = Math.Abs(ratio - (16.0 / 9.0)) < 0.02 ? "16:9" : $"{videoStream.Width}:{videoStream.Height}";
            }
  
            return new VideoAnalyzerOutput(
                SourceWidth: videoStream.Width,
                SourceHeight: videoStream.Height,
                DurationSeconds: Math.Round(duration.TotalSeconds, 2),
                DurationFormatted: durationFormatted,
                FrameRate: Math.Round(videoStream.FrameRate, 2),
                AspectRatio: aspectRatio,
                VideoCodec: videoStream.CodecName,
                AudioCodec: audioStream?.CodecName ?? "none",
                AudioBitrateKbps: (int)((audioStream?.BitRate ?? 0) / 1000),
                AudioSampleRate: audioStream?.SampleRateHz ?? 0
            );
        }

}
