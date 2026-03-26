using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Handles the actual transcoding logic when a StartVideoTranscodingCommand is received.
/// </summary>
public class StartVideoProcessingCommandHandler : IConsumer<StartVideoProcessingCommand>
{
    private readonly IAmazonMediaConvert _mediaConvertClient;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<StartVideoProcessingCommandHandler> _logger;
    private readonly IModuleBus _moduleBus;
    private readonly IClock _clock;

    public StartVideoProcessingCommandHandler(
        IAmazonMediaConvert mediaConvertClient,
        AWSResources aWSResources,
        ILogger<StartVideoProcessingCommandHandler> logger,
        IModuleBus moduleBus,
        IClock clock)
    {
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        _logger = logger;
        _moduleBus = moduleBus;
        _clock = clock;
    }

    public async Task Consume(ConsumeContext<StartVideoProcessingCommand> context)
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Error] MediaConvert Job failed for file: {Key}", key);
            throw;
        }
    }

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId)
    {
        var assembly = typeof(StartVideoProcessingCommandHandler).Assembly;
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
            { "sourceFile", inputS3 },
            { "outputPath" , outputPath},
        };

        return jobSettings;
    }
}
