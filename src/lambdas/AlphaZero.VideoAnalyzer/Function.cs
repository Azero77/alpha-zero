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

    internal static VideoAnalyzerOutput ParseFfprobeOutput(string stdout)
    {
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        int width = 1920, height = 1080, sampleRate = 44100, audioBitrateKbps = 128;
        string videoCodec = "h264", audioCodec = "aac";
        double frameRate = 30.0;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.GetProperty("codec_type").GetString();
                if (codecType == "video")
                {
                    width = stream.GetProperty("width").GetInt32();
                    height = stream.GetProperty("height").GetInt32();
                    videoCodec = stream.GetProperty("codec_name").GetString() ?? "h264";
                    if (stream.TryGetProperty("r_frame_rate", out var rFrameRate))
                        frameRate = ParseFrameRate(rFrameRate.GetString());
                }
                else if (codecType == "audio")
                {
                    audioCodec = stream.GetProperty("codec_name").GetString() ?? "aac";
                    if (stream.TryGetProperty("sample_rate", out var sr) && int.TryParse(sr.GetString(), out var parsedSr))
                        sampleRate = parsedSr;
                    if (stream.TryGetProperty("bit_rate", out var br) && int.TryParse(br.GetString(), out var parsedBr))
                        audioBitrateKbps = parsedBr / 1000;
                }
            }
        }

        double durationSeconds = 0.0;
        if (root.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var durProp))
        {
            double.TryParse(durProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds);
        }

        var ts = TimeSpan.FromSeconds(durationSeconds);
        var durationFormatted = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        var aspectRatio = (height > 0 && Math.Abs((double)width / height - (16.0 / 9.0)) < 0.02) ? "16:9" : $"{width}:{height}";

        return new VideoAnalyzerOutput(
            SourceWidth: width,
            SourceHeight: height,
            DurationSeconds: durationSeconds,
            DurationFormatted: durationFormatted,
            FrameRate: frameRate,
            AspectRatio: aspectRatio,
            VideoCodec: videoCodec,
            AudioCodec: audioCodec,
            AudioBitrateKbps: audioBitrateKbps,
            AudioSampleRate: sampleRate
        );
    }

    internal static double ParseFrameRate(string? rFrameRate)
    {
        if (string.IsNullOrWhiteSpace(rFrameRate)) return 30.0;
        var parts = rFrameRate.Split('/');
        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) 
                              && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) 
                              && den > 0)
            return Math.Round(num / den, 2);
        return double.TryParse(rFrameRate, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) ? fps : 30.0;
    }
}
