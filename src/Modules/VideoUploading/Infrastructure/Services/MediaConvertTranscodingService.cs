using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;
using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using Aspire.Shared;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class MediaConvertTranscodingService : IVideoTranscodingService
{
    private readonly IAmazonMediaConvert _mediaConvertClient;
    private readonly AWSResources _aWSResources;
    private readonly ILogger<MediaConvertTranscodingService> _logger;

    public MediaConvertTranscodingService(
        IAmazonMediaConvert mediaConvertClient,
        AWSResources aWSResources,
        ILogger<MediaConvertTranscodingService> _logger)
    {
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        this._logger = _logger;
    }

    public async Task<ErrorOr<string>> StartTranscodingJobAsync(
        Guid videoId, 
        string inputS3Uri, 
        string outputPathS3Uri, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobRequest = CreateJobRequestFromTemplate(inputS3Uri, outputPathS3Uri, videoId.ToString());
            var response = await _mediaConvertClient.CreateJobAsync(jobRequest, cancellationToken);

            _logger.LogInformation("[MediaConvert] Job Created: {JobId} for Video: {VideoId}", 
                response.Job.Id, videoId);

            return response.Job.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Error] MediaConvert Job failed for Video: {VideoId}", videoId);
            return Error.Failure("Transcoding.JobCreationFailed", ex.Message);
        }
    }

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId)
    {
        var assembly = typeof(MediaConvertTranscodingService).Assembly;
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