using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using MassTransit;
using Microsoft.Extensions.Logging;

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
        if (context.Message.Records == null)
        {
            _logger.LogDebug("Received S3 event with no records.");
            return;
        }

        foreach (var record in context.Message.Records)
        {
            if (record.EventName.Value.StartsWith("ObjectCreated:"))
            {
                string key = record.S3.Object.Key;
                string bucketName = record.S3.Bucket.Name;
                
                // Skip objects that look like folders or non-video files if necessary, 
                // but at least ensure we handle metadata correctly.
                var metadataRequest = new GetObjectMetadataRequest()
                {
                    BucketName = bucketName,
                    Key = key,
                };

                var metadataResponse = await _s3.GetObjectMetadataAsync(metadataRequest);
                if (metadataResponse is null || metadataResponse.Metadata is null)
                {
                    await _moduleBus.Publish(new VideoProcessingFailedEvent(Guid.Empty, "Invalid Metadata", key));
                    _logger.LogCritical("Video Uploaded with no metadata, key : {key}", key);
                    continue;
                }

                // S3 Metadata keys are often normalized to lowercase by AWS.
                // We try both the original case and lowercase to be safe.
                string? videoId = metadataResponse.Metadata[S3UploadService.VideoIdMetaDataHeader] 
                               ?? metadataResponse.Metadata[S3UploadService.VideoIdMetaDataHeader.ToLowerInvariant()];
                
                string? tenantIdStr = metadataResponse.Metadata["TenantId"] 
                                  ?? metadataResponse.Metadata["tenantid"];
                
                string? targetResourceArn = metadataResponse.Metadata["TargetResourceArn"] 
                                        ?? metadataResponse.Metadata["targetresourcearn"];

                if (videoId is null || !Guid.TryParse(videoId, out Guid videoGuid) ||
                    tenantIdStr is null || !Guid.TryParse(tenantIdStr, out Guid tenantGuid))
                {
                    // If it's a file we don't care about (e.g. MediaConvert outputs), just skip without error if it's in the output bucket.
                    // But for now, let's keep the warning but make it clearer.
                    _logger.LogWarning("Video with missing or invalid metadata has been found, Key : {key}. VideoId: {VideoId}, TenantId: {TenantId}", 
                        key, videoId, tenantIdStr);
                    continue;
                }

                await _moduleBus.Publish(new VideoDeliveredToInputEvent(videoGuid, key, bucketName, tenantGuid, targetResourceArn));
            }
            else if (record.EventName.Value.StartsWith("ObjectRemoved:"))
            {
                string key = record.S3.Object.Key;
                _logger.LogInformation("Video file removed from S3: {key}", key);
                await _moduleBus.Publish(new VideoDeletedFromS3Event(key));
            }
        }
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
