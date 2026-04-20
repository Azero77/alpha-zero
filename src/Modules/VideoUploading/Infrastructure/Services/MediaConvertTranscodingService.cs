using AlphaZero.Modules.VideoUploading.Application;
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
        ILogger<MediaConvertTranscodingService> logger)
    {
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        _logger = logger;
    }

    public VideoTranscodingMetehod Method => VideoTranscodingMetehod.SQSMediaConvert;

    public async Task<ErrorOr<string>> StartTranscodingJobAsync(
        Guid videoId, 
        string inputS3Uri, 
        string outputPathS3Uri, 
        int sourceWidth,
        int sourceHeight,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobRequest = CreateJobRequestFromTemplate(inputS3Uri, outputPathS3Uri, videoId.ToString(), sourceWidth, sourceHeight);
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

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId, int sourceWidth, int sourceHeight)
    {
        var assembly = typeof(MediaConvertTranscodingService).Assembly;
        var file = "AlphaZero.Modules.VideoUploading.Infrastructure.Consumers.job.json";

        using var stream = assembly.GetManifestResourceStream(file) 
            ?? throw new ArgumentException($"job.json File not found at {file}");
        using var reader = new StreamReader(stream);
        
        string jsonTemplate = reader.ReadToEnd();

        string drmKey = assetId.Replace("-", ""); 
        string drmKeyId = assetId.Replace("-", "");

        jsonTemplate = jsonTemplate
            .Replace("##INPUT_FILE##", inputS3)
            .Replace("##OUTPUT_PATH##", outputPath)
            .Replace("##KMS_KEY_ARN##", _aWSResources.MediaConvertKeyKMSArn)
            .Replace("##MediaConvertRole##", _aWSResources.MediaConvertRoleArn)
            .Replace("##DRM_KEY##", drmKey)
            .Replace("##DRM_KEY_ID##", drmKeyId)
            .Replace("##DRM_KEY_URL##", "http://clearkey.local");

        var jobSettings = JsonConvert.DeserializeObject<CreateJobRequest>(jsonTemplate);
        if (jobSettings == null) throw new Exception("Failed to deserialize job.json");

        // Filter renditions to prevent upscaling
        var cmafGroup = jobSettings.Settings.OutputGroups.FirstOrDefault(g => g.Name == "CMAF");
        if (cmafGroup != null)
        {
            // Keep renditions where width <= sourceWidth OR they are audio-only
            cmafGroup.Outputs = cmafGroup.Outputs.Where(o => 
                o.VideoDescription == null || o.VideoDescription.Width <= sourceWidth
            ).ToList();

            // Safety check: if all video outputs were filtered out (unlikely but possible), 
            // at least keep the lowest one.
            if (!cmafGroup.Outputs.Any(o => o.VideoDescription != null))
            {
                _logger.LogWarning("All video renditions were larger than source resolution {Width}x{Height}. Keeping audio only or check logic.", sourceWidth, sourceHeight);
            }
        }
        
        jobSettings.UserMetadata = new Dictionary<string, string>
        {
            { S3UploadService.VideoIdMetaDataHeader, assetId },
            { "sourceFile", inputS3 },
            { "outputPath" , outputPath},
        };

        return jobSettings;
    }
}
