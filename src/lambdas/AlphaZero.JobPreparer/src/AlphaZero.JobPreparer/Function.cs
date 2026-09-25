using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AlphaZero.VideoPipeline.Exceptions;

[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<AlphaZero.JobPreparer.JobPreparerJsonContext>))]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AlphaZero.JobPreparer.Tests")]

namespace AlphaZero.JobPreparer;

public record SourceMetadata(
    int SourceWidth,
    int SourceHeight,
    double DurationSeconds,
    string DurationFormatted,
    double FrameRate,
    string AspectRatio,
    string VideoCodec,
    string AudioCodec);

public record JobPreparerInput(
    string VideoId,
    string TenantId,
    string SourceBucket,
    string SourceKey,
    string TransientOutputBucket,
    string? TranscodingEngine,
    string? EncryptionMethod,
    string? TargetResourceArn,
    SourceMetadata Metadata);

public record JobPreparerOutput(
    string JobConfigS3Uri,
    string JobConfigKey,
    string SourceBucket,
    string SourceKey,
    string TransientOutputBucket,
    string OutputPrefix,
    string TranscodingEngine,
    int SourceWidth,
    int SourceHeight,
    string DurationFormatted,
    string? TargetResourceArn);

public sealed record TranscodingJobInput
{
    public required Guid VideoId { get; init; }
    public required Guid TenantId { get; init; }
    public required string SourcePath { get; init; }
    public required string OutputPrefix { get; init; }
    public required VideoMetadata SourceMetadata { get; init; }
    public required TranscodeSettings Settings { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<EncryptionMethod>))]
    public EncryptionMethod EncryptionMethod { get; init; } = EncryptionMethod.None;

    public EncryptionSettings? Encryption { get; init; }
    public string? ThumbnailRelativeUrl { get; init; }
}

public sealed record VideoMetadata(
    int SourceWidth,
    int SourceHeight,
    TimeSpan Duration);

public sealed record TranscodeSettings
{
    public required OutputPreset[] Outputs { get; init; }
    public int SegmentLengthSeconds { get; init; } = 6;
    public int FragmentLengthSeconds { get; init; } = 2;
    public AudioSettings Audio { get; init; } = AudioSettings.Default;
}

public sealed record OutputPreset(
    int Width,
    int Height,
    int MaxBitrateKbps,
    int QvbrQualityLevel,
    string NameModifier);

public sealed record AudioSettings(
    string Codec,
    int BitrateKbps,
    int SampleRate)
{
    public static readonly AudioSettings Default = new("aac", 128, 44100);
}

public sealed record EncryptionSettings(
    string KeyId,
    string Key,
    string? KeyUrl);

[JsonConverter(typeof(JsonStringEnumConverter<EncryptionMethod>))]
public enum EncryptionMethod
{
    None = 0,
    ClearKey = 1
}

public class Function
{
    private static readonly IAmazonS3 S3Client = new AmazonS3Client();
    private static readonly IAmazonSimpleSystemsManagement SsmClient = new AmazonSimpleSystemsManagementClient();
    private static string? _masterSecretCache;

    public static async Task Main()
    {
        Func<JobPreparerInput, ILambdaContext, Task<JobPreparerOutput>> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<JobPreparerJsonContext>())
            .Build()
            .RunAsync();
    }

    private static async Task<string> GetMasterSecretAsync()
    {
        if (_masterSecretCache != null) return _masterSecretCache;
        var request = new GetParameterRequest { Name = "/AlphaZero/VideoPipeline/MasterClearKey", WithDecryption = true };
        var response = await SsmClient.GetParameterAsync(request);
        _masterSecretCache = response.Parameter.Value;
        return _masterSecretCache;
    }

    public static async Task<JobPreparerOutput> FunctionHandler(JobPreparerInput input, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation($"[JobPreparer] Generating job.json for VideoId {input.VideoId}");

            var outputPrefix = $"{input.TenantId}/{input.VideoId}/";
            var jobKey = $"{input.TenantId}/{input.VideoId}/job.json";

            var ladder = BuildAdaptiveLadder(input.Metadata.SourceWidth, input.Metadata.SourceHeight);

            EncryptionSettings? encryption = null;
            var encMethod = string.Equals(input.EncryptionMethod, "ClearKey", StringComparison.OrdinalIgnoreCase)
                ? EncryptionMethod.ClearKey
                : EncryptionMethod.None;

            if (encMethod == EncryptionMethod.ClearKey)
            {
                var masterSecret = await GetMasterSecretAsync();
                var clearKey = GenerateClearKeySecret(masterSecret, input.VideoId);
                var keyId = input.VideoId.Replace("-", "").ToLowerInvariant();
                encryption = new EncryptionSettings(
                    KeyId: keyId,
                    Key: clearKey,
                    KeyUrl: $"/api/video/keys/{input.VideoId}"
                );
            }

            var jobInput = new TranscodingJobInput
            {
                VideoId = Guid.TryParse(input.VideoId, out var vId) ? vId : Guid.NewGuid(),
                TenantId = Guid.TryParse(input.TenantId, out var tId) ? tId : Guid.NewGuid(),
                SourcePath = input.SourceKey,
                OutputPrefix = outputPrefix,
                SourceMetadata = new VideoMetadata(
                    SourceWidth: input.Metadata.SourceWidth,
                    SourceHeight: input.Metadata.SourceHeight,
                    Duration: TimeSpan.FromSeconds(input.Metadata.DurationSeconds)
                ),
                Settings = new TranscodeSettings
                {
                    Outputs = ladder,
                    SegmentLengthSeconds = 6,
                    FragmentLengthSeconds = 2,
                    Audio = AudioSettings.Default
                },
                EncryptionMethod = encMethod,
                Encryption = encryption,
                ThumbnailRelativeUrl = null
            };

            var json = JsonSerializer.Serialize(jobInput, JobPreparerJsonContext.Default.TranscodingJobInput);

            await S3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = input.SourceBucket,
                Key = jobKey,
                ContentBody = json,
                ContentType = "application/json"
            });

            return new JobPreparerOutput(
                JobConfigS3Uri: $"s3://{input.SourceBucket}/{jobKey}",
                JobConfigKey: jobKey,
                SourceBucket: input.SourceBucket,
                SourceKey: input.SourceKey,
                TransientOutputBucket: input.TransientOutputBucket,
                OutputPrefix: outputPrefix,
                TranscodingEngine: input.TranscodingEngine ?? "FFMPEG",
                SourceWidth: input.Metadata.SourceWidth,
                SourceHeight: input.Metadata.SourceHeight,
                DurationFormatted: input.Metadata.DurationFormatted,
                TargetResourceArn: input.TargetResourceArn
            );
        }
        catch (AmazonSimpleSystemsManagementException ex)
        {
            throw new TransientException("Failed to retrieve parameters.", ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw new TransientException("S3 error during job prep.", ex);
        }
        catch (Exception ex)
        {
            throw new VideoProcessingException($"Job prep failed: {ex.Message}", ex);
        }
    }

    internal static OutputPreset[] BuildAdaptiveLadder(int width, int height)
    {
        var presets = new List<OutputPreset>
        {
            new OutputPreset(640, 360, 600, 7, "_360p")
        };

        if (height >= 480)
            presets.Add(new OutputPreset(854, 480, 1200, 7, "_480p"));

        if (height >= 720)
            presets.Add(new OutputPreset(1280, 720, 2400, 7, "_720p"));

        if (height >= 1080)
            presets.Add(new OutputPreset(1920, 1080, 4500, 7, "_1080p"));

        return presets.ToArray();
    }

    internal static string GenerateClearKeySecret(string masterSecret, string videoId)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(masterSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(videoId));
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }
}

[JsonSerializable(typeof(SourceMetadata))]
[JsonSerializable(typeof(JobPreparerInput))]
[JsonSerializable(typeof(JobPreparerOutput))]
[JsonSerializable(typeof(TranscodingJobInput))]
[JsonSerializable(typeof(VideoMetadata))]
[JsonSerializable(typeof(TranscodeSettings))]
[JsonSerializable(typeof(OutputPreset))]
[JsonSerializable(typeof(OutputPreset[]))]
[JsonSerializable(typeof(AudioSettings))]
[JsonSerializable(typeof(EncryptionSettings))]
[JsonSerializable(typeof(EncryptionMethod))]
public partial class JobPreparerJsonContext : JsonSerializerContext { }
