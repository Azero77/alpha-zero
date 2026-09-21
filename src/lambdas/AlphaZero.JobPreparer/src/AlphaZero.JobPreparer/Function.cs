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

public class JobInputConfig
{
    public string file { get; set; }
    public int videoStreamIndex { get; set; }
    public int audioStreamIndex { get; set; }
}

public class JobOutputConfig
{
    public string bucket { get; set; }
    public string prefix { get; set; }
    public int segmentDurationSeconds { get; set; }
    public bool enableThumbnails { get; set; }
    public int thumbnailTimeOffsetSeconds { get; set; }
}

public class JobCencConfig
{
    public bool enabled { get; set; }
    public string scheme { get; set; }
    public string keyId { get; set; }
    public string key { get; set; }
}

public class RenditionConfig
{
    public string name { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public int videoBitrateKbps { get; set; }
    public int audioBitrateKbps { get; set; }
}

public class JobConfig
{
    public string version { get; set; }
    public string jobId { get; set; }
    public string tenantId { get; set; }
    public JobInputConfig[] inputs { get; set; }
    public JobOutputConfig output { get; set; }
    public RenditionConfig[] ladder { get; set; }
    public JobCencConfig? cenc { get; set; }
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

            string? clearKey = null;
            if (input.EncryptionMethod == "ClearKey")
            {
                var masterSecret = await GetMasterSecretAsync();
                clearKey = GenerateClearKeySecret(masterSecret, input.VideoId);
            }

            var jobConfig = new JobConfig
            {
                version = "1.0",
                jobId = input.VideoId,
                tenantId = input.TenantId,
                inputs = new[]
                {
                    new JobInputConfig {
                        file = $"s3://{input.SourceBucket}/{input.SourceKey}",
                        videoStreamIndex = 0,
                        audioStreamIndex = 1
                    }
                },
                output = new JobOutputConfig
                {
                    bucket = input.TransientOutputBucket,
                    prefix = outputPrefix,
                    segmentDurationSeconds = 6,
                    enableThumbnails = true,
                    thumbnailTimeOffsetSeconds = 2
                },
                ladder = ladder,
                cenc = (input.EncryptionMethod == "ClearKey") ? new JobCencConfig
                {
                    enabled = true,
                    scheme = "cbcs",
                    keyId = input.VideoId.Replace("-", "").ToLowerInvariant(),
                    key = clearKey
                } : null
            };

            var json = JsonSerializer.Serialize(jobConfig, JobPreparerJsonContext.Default.JobConfig);

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

    internal static RenditionConfig[] BuildAdaptiveLadder(int width, int height)
    {
        var renditions = new List<RenditionConfig>
        {
            new RenditionConfig { name = "360p", width = 640, height = 360, videoBitrateKbps = 600, audioBitrateKbps = 96 }
        };

        if (height >= 480)
            renditions.Add(new RenditionConfig { name = "480p", width = 854, height = 480, videoBitrateKbps = 1200, audioBitrateKbps = 128 });

        if (height >= 720)
            renditions.Add(new RenditionConfig { name = "720p", width = 1280, height = 720, videoBitrateKbps = 2400, audioBitrateKbps = 128 });

        if (height >= 1080)
            renditions.Add(new RenditionConfig { name = "1080p", width = 1920, height = 1080, videoBitrateKbps = 4500, audioBitrateKbps = 192 });

        return renditions.ToArray();
    }

    internal static string GenerateClearKeySecret(string masterSecret, string videoId)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(masterSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(videoId));
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }
}

[JsonSerializable(typeof(JobPreparerInput))]
[JsonSerializable(typeof(JobPreparerOutput))]
[JsonSerializable(typeof(JobConfig))]
public partial class JobPreparerJsonContext : JsonSerializerContext { }
