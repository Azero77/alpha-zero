using AlphaZero.Modules.VideoUploading.IntegrationEvents;
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


public class Function
{
    private static readonly IAmazonS3 S3Client = new AmazonS3Client();
    private static readonly IAmazonSimpleSystemsManagement SsmClient = new AmazonSimpleSystemsManagementClient();
    private static readonly Amazon.SQS.IAmazonSQS SqsClient = new Amazon.SQS.AmazonSQSClient();
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


    private static async Task NotifyProgressAsync(string videoId, string tenantId, string stage, string status)
    {
        var queueUrl = Environment.GetEnvironmentVariable("PROGRESS_QUEUE_URL");
        if (string.IsNullOrEmpty(queueUrl)) return;

        var message = new VideoProgressQueueMessage(
            VideoId: videoId,
            TenantId: tenantId,
            Stage: stage,
            Status: status
        );

        var request = new Amazon.SQS.Model.SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = JsonSerializer.Serialize(message, JobPreparerJsonContext.Default.VideoProgressQueueMessage)
        };
        await SqsClient.SendMessageAsync(request);
    }

    public static async Task<JobPreparerOutput> FunctionHandler(JobPreparerInput input, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation($"[JobPreparer] Generating job.json for VideoId {input.VideoId}");
            await NotifyProgressAsync(input.VideoId, input.TenantId, "preparing", "IN_PROGRESS");

            var outputPrefix = $"{input.TenantId}/{input.VideoId}/";
            var jobKey = $"{input.TenantId}/{input.VideoId}/job.json";

            var ladder = BuildAdaptiveLadder(input.SourceMetadata.SourceWidth, input.SourceMetadata.SourceHeight);

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

            string? thumbnailRelativeUrl = null;
            if (!input.IsDefaultThumbnail)
            {
                thumbnailRelativeUrl = $"{input.TenantId}/{input.VideoId}/thumbnail";
            }

            var jobInput = new TranscodingJobInput
            {
                VideoId = Guid.TryParse(input.VideoId, out var vId) ? vId : Guid.NewGuid(),
                TenantId = Guid.TryParse(input.TenantId, out var tId) ? tId : Guid.NewGuid(),
                SourcePath = input.SourceKey,
                OutputPrefix = outputPrefix,
                SourceMetadata = new VideoMetadata(
                    SourceWidth: input.SourceMetadata.SourceWidth,
                    SourceHeight: input.SourceMetadata.SourceHeight,
                    Duration: TimeSpan.FromSeconds(input.SourceMetadata.DurationSeconds)
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
                ThumbnailRelativeUrl = thumbnailRelativeUrl
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
                SourceWidth: input.SourceMetadata.SourceWidth,
                SourceHeight: input.SourceMetadata.SourceHeight,
                DurationFormatted: input.SourceMetadata.DurationFormatted,
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
[JsonSerializable(typeof(VideoProgressQueueMessage))]
public partial class JobPreparerJsonContext : JsonSerializerContext { }
