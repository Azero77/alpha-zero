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
                    continue;
                }

                await context.Publish(new VideoDeliveredToInputEvent(key, bucketName,videoGuid));
                continue;
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
