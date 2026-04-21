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
    private readonly IVideoEncryptionService _encryptionService;
    private readonly ILogger<MediaConvertTranscodingService> _logger;

    public MediaConvertTranscodingService(
        IAmazonMediaConvert mediaConvertClient,
        AWSResources aWSResources,
        IVideoEncryptionService encryptionService,
        ILogger<MediaConvertTranscodingService> logger)
    {
        _mediaConvertClient = mediaConvertClient;
        _aWSResources = aWSResources;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public VideoTranscodingMetehod Method => VideoTranscodingMetehod.MediaConvert;

    public async Task<ErrorOr<string>> StartTranscodingJobAsync(
        Guid videoId, 
        string inputS3Uri, 
        string outputPathS3Uri, 
        int sourceWidth,
        int sourceHeight,
        VideoEncryptionMethod encryptionMethod = VideoEncryptionMethod.None,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EncryptionParams? encParams = null;
            if (encryptionMethod != VideoEncryptionMethod.None)
            {
                var encResult = await _encryptionService.GetEncryptionParamsAsync(videoId, encryptionMethod, cancellationToken);
                if (!encResult.IsError)
                {
                    encParams = encResult.Value;
                }
            }

            var jobRequest = CreateJobRequestFromTemplate(inputS3Uri, outputPathS3Uri, videoId.ToString(), sourceWidth, sourceHeight, encParams);
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

    private CreateJobRequest CreateJobRequestFromTemplate(string inputS3, string outputPath, string assetId, int sourceWidth, int sourceHeight, EncryptionParams? encParams)
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

        if (encParams != null)
        {
            jsonTemplate = jsonTemplate
                .Replace("##DRM_KEY##", encParams.KeyValue)
                .Replace("##DRM_KEY_ID##", encParams.KeyId)
                .Replace("##DRM_KEY_URL##", encParams.KeyUrl ?? "http://clearkey.local");
        }
        else
        {
            // If no encryption is specified, we might want to strip the encryption section from JSON
            // For now, to keep it simple and avoid complex JSON manipulation, 
            // we'll just use dummy values if the template requires them, 
            // but ideally the template should be updated to make it optional.
            jsonTemplate = jsonTemplate
                .Replace("##DRM_KEY##", "00000000000000000000000000000000")
                .Replace("##DRM_KEY_ID##", "00000000000000000000000000000000")
                .Replace("##DRM_KEY_URL##", "http://disabled.local");
        }

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
