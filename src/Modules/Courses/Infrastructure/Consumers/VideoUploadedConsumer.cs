using Amazon.EventBridge.Model;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Util;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public record VideoUploadedEvent(string Key, string Bucket);

public class MediaConverterVideoUploadedEventHandler : IConsumer<VideoUploadedEvent>
{
    private readonly IConfiguration _configuration;
    private readonly IAmazonMediaConvert _mediaConvertClient;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<MediaConverterVideoUploadedEventHandler> _logger;

    public MediaConverterVideoUploadedEventHandler(IConfiguration configuration,
        IAmazonMediaConvert mediaConvertClient,
        AWSResources aWSResources,
        ILogger<MediaConverterVideoUploadedEventHandler> logger)
    {
        _configuration = configuration;
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VideoUploadedEvent> context)
    {
        
        var message = context.Message;
        var assetId = Guid.NewGuid().ToString();

        // 1. Setup paths
        string sourceS3 = $"s3://{message.Bucket}/{message.Key}";
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(message.Key);
        string destinationBucket = _aWSResources.OutputS3?.BucketName ?? throw new ArgumentException();
        string outputPath = $"s3://{destinationBucket}/assets/{assetId}";

        try
        {

            // 4. Load and Prepare Job Settings
            var jobRequest = CreateJobRequestFromTemplate(sourceS3, outputPath, assetId);

            // 5. Submit Job
            var response = await _mediaConvertClient.CreateJobAsync(jobRequest);

            _logger.LogInformation("[MediaConvert] Job Created: {response} for Asset: {assetId}",response.Job.Id,assetId);
            
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Error] MediaConvert Job failed: {message}", ex.Message);
            throw; // Let MassTransit handle retries
        }
    }

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId)
    {
        // Load your JSON file
        var jsonTemplate = File.ReadAllText("job.json");

        // Replace placeholders (Simple string replacement or deserialization)
        jsonTemplate = jsonTemplate
            .Replace("##INPUT_FILE##", inputS3)
            .Replace("##OUTPUT_PATH##", outputPath)
            .Replace("##KMS_KEY_ARN##", _aWSResources.MediaConvertKeyKMSArn)
            .Replace("##MediaConvertRole##",_aWSResources.MediaConvertRoleArn);

        // Deserialize the JSON into the SDK's Request object
        var jobSettings = JsonSerializer.Deserialize<CreateJobRequest>(jsonTemplate, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (jobSettings == null) throw new Exception("Failed to deserialize job.json");

        jobSettings.UserMetadata = new Dictionary<string, string>
        {
            { "assetID", assetId },
            { "sourceFile", inputS3 }
        };

        return jobSettings;
    }
}