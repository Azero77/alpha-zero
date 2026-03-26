using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;


/// <summary>
/// Handles the raw S3 Notification from SQS and converts it to a clean internal event.
/// </summary>
public class SQSS3EventHandler : IConsumer<S3EventNotification>
{
    private readonly ILogger<SQSS3EventHandler> _logger;
    private readonly IAmazonS3 _s3;
    private readonly IModuleBus _moduleBus;

    public SQSS3EventHandler(ILogger<SQSS3EventHandler> logger, IAmazonS3 s3, IModuleBus moduleBus)
    {
        _logger = logger;
        _s3 = s3;
        _moduleBus = moduleBus;
    }

    public async Task Consume(ConsumeContext<S3EventNotification> context)
    {
        foreach (var record in context.Message.Records)
        {
            if (record.EventName.Value.StartsWith("ObjectCreated:"))
            {

                string key = record.S3.Object.Key;
                string bucketName = record.S3.Bucket.Name;
                var metadataRequest = new GetObjectMetadataRequest()
                {
                    BucketName = bucketName,
                    Key = key,
                };

                var metadataResponse = await _s3.GetObjectMetadataAsync(metadataRequest);
                string? videoId = metadataResponse.Metadata?[S3UploadService.VideoIdMetaDataHeader];
                if (metadataResponse is null || videoId is null || !Guid.TryParse(videoId,out Guid videoGuid))
                {
                    _logger.LogError("Video with no metadata has been found, Key : {key}", key);
                    await _moduleBus.Publish(new VideoUploadFailedEvent(null,key));
                    continue;
                }

                await context.Publish(new VideoUploadedToInputEvent(key, bucketName,videoGuid));
                continue;
            }
        }
    }
}

/// <summary>
/// Handles the actual transcoding logic when a VideoUploaded event is received.
/// </summary>
public class VideoUploadedEventHandler : IConsumer<VideoUploadedToInputEvent>
{
    private readonly IAmazonMediaConvert _mediaConvertClient;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<VideoUploadedEventHandler> _logger;
    private readonly IModuleBus _moduleBus;

    public VideoUploadedEventHandler(
        IAmazonMediaConvert mediaConvertClient,
        AWSResources aWSResources,
        ILogger<VideoUploadedEventHandler> logger,
        IModuleBus moduleBus)
    {
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        _logger = logger;
        _moduleBus = moduleBus;
    }

    public async Task Consume(ConsumeContext<VideoUploadedToInputEvent> context)
    {
        var key = context.Message.Key;
        var bucket = context.Message.BucketName;
        var assetId = context.Message.VideoId.ToString();

        _logger.LogInformation("[Transcoding] Starting job for Asset: {AssetId}", assetId);

        string sourceS3 = $"s3://{bucket}/{key}";
        string destinationBucket = _aWSResources.OutputS3?.BucketName 
            ?? throw new ArgumentException("Output S3 bucket is not configured");
        string outputPath = $"s3://{destinationBucket}/streaming/{assetId}";

        try
        {
            var jobRequest = CreateJobRequestFromTemplate(sourceS3, outputPath, assetId);
            var response = await _mediaConvertClient.CreateJobAsync(jobRequest);

            _logger.LogInformation("[MediaConvert] Job Created: {JobId} for Asset: {AssetId}", 
                response.Job.Id, assetId);
            await _moduleBus.Publish(new VideoProcessingStartedEvent(key,bucket,context.Message.VideoId,response.Job.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Error] MediaConvert Job failed for file: {Key}", key);
            throw;
        }
    }

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId)
    {
        var assembly = typeof(VideoUploadedEventHandler).Assembly;
        var file = "AlphaZero.Modules.VideoUploading.Infrastructure.Consumers.job.json";

        using var stream = assembly.GetManifestResourceStream(file) 
            ?? throw new ArgumentException($"job.json File not found at {file}");
        using var reader = new StreamReader(stream);
        
        string jsonTemplate = reader.ReadToEnd();
        jsonTemplate = jsonTemplate
            .Replace("##INPUT_FILE##", inputS3)
            .Replace("##OUTPUT_PATH##", outputPath)
            .Replace("##KMS_KEY_ARN##", _aWSResources.MediaConvertKeyKMSArn)
            .Replace("##MediaConvertRole##", _aWSResources.MediaConvertRoleArn);

        var jobSettings = JsonConvert.DeserializeObject<CreateJobRequest>(jsonTemplate);
        if (jobSettings == null) throw new Exception("Failed to deserialize job.json");
        jobSettings.UserMetadata = new Dictionary<string, string>
        {
            { S3UploadService.VideoIdMetaDataHeader, assetId },
            { "sourceFile", inputS3 }
        };

        return jobSettings;
    }
}

/// <summary>
/// Maps the SQSS3EventHandler to the specific AWS SQS Queue and raw JSON format.
/// </summary>
public class SQSS3EventHandlerDefinition : ConsumerDefinition<SQSS3EventHandler>
{
    public SQSS3EventHandlerDefinition()
    {
        EndpointName = "VideoUploadedQueue";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, 
        IConsumerConfigurator<SQSS3EventHandler> consumerConfigurator, 
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}
