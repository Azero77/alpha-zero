using Amazon.EventBridge.Model;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class MediaConverterSQSVideoUploadedEventHandler : IConsumer<S3EventNotification>
{
    private readonly IConfiguration _configuration;
    private readonly IAmazonMediaConvert _mediaConvertClient;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<MediaConverterSQSVideoUploadedEventHandler> _logger;

    public MediaConverterSQSVideoUploadedEventHandler(IConfiguration configuration,
        IAmazonMediaConvert mediaConvertClient,
        AWSResources aWSResources,
        ILogger<MediaConverterSQSVideoUploadedEventHandler> logger)
    {
        _configuration = configuration;
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<S3EventNotification> context)
    {
            
        foreach (var record in context.Message.Records)
        {
            if (!record.EventName.Value.StartsWith("ObjectCreated:"))
            {
                _logger.LogInformation("Skipping event {EventName} for key {Key}", record.EventName, record.S3.Object.Key);
                continue;
            }

            var key = record.S3.Object.Key;
            var bucket = record.S3.Bucket.Name;
            var assetId = Guid.NewGuid().ToString();

            // 1. Setup paths
            string sourceS3 = $"s3://{bucket}/{key}";
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(key);
            string destinationBucket = _aWSResources.OutputS3?.BucketName ?? throw new ArgumentException();
            string outputPath = $"s3://{destinationBucket}/streaming/{assetId}";

            try
            {

                var jobRequest = CreateJobRequestFromTemplate(sourceS3, outputPath, assetId);

                var response = await _mediaConvertClient.CreateJobAsync(jobRequest);

                _logger.LogInformation("[MediaConvert] Job Created: {response} for Asset: {assetId}", response.Job.Id, assetId);

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Error] MediaConvert Job failed for file: {key}", key);
                throw; // Let MassTransit handle retries
            }
        }
    }

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId)
    {
        // Load your JSON file
        var assembly = typeof(MediaConverterSQSVideoUploadedEventHandler).Assembly;
        var file = $"AlphaZero.Modules.Courses.Infrastructure.Consumers.job.json";

        using var stream = assembly.GetManifestResourceStream(file) ?? throw new ArgumentException(" job.json File not found");
        using var reader = new StreamReader(stream);
        string jsonTemplate = reader.ReadToEnd();
        jsonTemplate = jsonTemplate
            .Replace("##INPUT_FILE##", inputS3)
            .Replace("##OUTPUT_PATH##", outputPath)
            .Replace("##KMS_KEY_ARN##", _aWSResources.MediaConvertKeyKMSArn)
            .Replace("##MediaConvertRole##",_aWSResources.MediaConvertRoleArn);

        var jobSettings = JsonConvert.DeserializeObject<CreateJobRequest>(jsonTemplate);

        if (jobSettings == null) throw new Exception("Failed to deserialize job.json");

        jobSettings.UserMetadata = new Dictionary<string, string>
        {
            { "assetID", assetId },
            { "sourceFile", inputS3 }
        };

        return jobSettings;
    }
}

public class MediaConverterSQSVideoUploadedEventHandlerDefinition
    : ConsumerDefinition<MediaConverterSQSVideoUploadedEventHandler>
{
    public MediaConverterSQSVideoUploadedEventHandlerDefinition()
    {
        EndpointName = "VideoUploadedQueue";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<MediaConverterSQSVideoUploadedEventHandler> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}