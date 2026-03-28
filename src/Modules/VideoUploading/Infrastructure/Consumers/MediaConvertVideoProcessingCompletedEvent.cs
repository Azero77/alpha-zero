using AlphaZero.Modules.VideoUploading.Application;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using ErrorOr;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Consumers;

/// <summary>
/// Model for MediaConvert Job State Change event from EventBridge
/// </summary>
public class MediaConvertJobEvent
{
    [JsonProperty("detail")]
    public MediaConvertJobDetail Detail { get; set; } = null!;
}

public class MediaConvertJobDetail
{
    [JsonProperty("jobId")]
    public string JobId { get; set; } = null!;

    [JsonProperty("status")]
    public string Status { get; set; } = null!;

    [JsonProperty("userMetadata")]
    public Dictionary<string, string> UserMetadata { get; set; } = null!;
}

/// <summary>
/// Separate class for listening to jobs and fire the internal events.
/// This handles the raw EventBridge events from SQS.
/// </summary>
public class SQSMediaConverterJobStatusEventHandler : 
    IConsumer<MediaConvertJobEvent>
{
    private readonly IModuleBus _moduleBus;
    private readonly ILogger<SQSMediaConverterJobStatusEventHandler> _logger;

    public SQSMediaConverterJobStatusEventHandler(IModuleBus moduleBus, ILogger<SQSMediaConverterJobStatusEventHandler> logger)
    {
        _moduleBus = moduleBus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MediaConvertJobEvent> context)
    {
        var detail = context.Message.Detail;
        
        if (!detail.UserMetadata.TryGetValue(S3UploadService.VideoIdMetaDataHeader, out var videoIdStr) ||
            !Guid.TryParse(videoIdStr, out var videoId))
        {
            _logger.LogCritical("MediaConvert Job {JobId} Status {Status} for Video with no Id", detail.JobId, detail.Status);
            return;
        }

        switch (detail.Status)
        {
            case "PROGRESSING":
                _logger.LogInformation("MediaConvert Job {JobId} Started for Video {VideoId}", detail.JobId, videoId);
                await _moduleBus.Publish(new VideoTranscodingStartedEvent(videoId, detail.JobId));
                break;

            case "COMPLETE":
                _logger.LogInformation("MediaConvert Job {JobId} completed for Video {VideoId}", detail.JobId, videoId);
                
                if (detail.UserMetadata.TryGetValue("outputPath", out var outputPath) &&
                    TryGetParamsForInputPath(outputPath, out string key, out string bucket))
                {
                    // MediaConvert outputPath includes s3://bucket/streaming/videoId/
                    // Our event needs the key prefix (streaming/videoId/)
                    await _moduleBus.Publish(new VideoTranscodingFinishedEvent(videoId, key, bucket));
                }
                else
                {
                    _logger.LogCritical("MediaConvert Job {JobId} completed but outputPath missing in metadata for Video {VideoId}", detail.JobId, videoId);
                    await _moduleBus.Publish(new VideoProcessingFailedEvent(videoId, "Output path missing after transcoding", null));
                }
                break;

            case "ERROR":
                _logger.LogError("MediaConvert Job {JobId} failed for Video {VideoId}", detail.JobId, videoId);
                await _moduleBus.Publish(new VideoProcessingFailedEvent(videoId, "MediaConvert job failed", null));
                break;
        }
    }

    private bool TryGetParamsForInputPath(string? inputPath, out string key, out string bucket)
    {
        key = string.Empty;
        bucket = string.Empty;
        if (inputPath is null)
            return false;
        
        // Match s3://bucketname/key/prefix/
        var regex = new Regex("^s3://([^/]+)/(.+)$");
        var match = regex.Match(inputPath);

        if (!match.Success) return false;
        
        bucket = match.Groups[1].Value;
        key = match.Groups[2].Value;

        return true;
    }
}

public class SQSMediaConverterJobStatusEventHandlerDefinition :
    ConsumerDefinition<SQSMediaConverterJobStatusEventHandler>
{
    public SQSMediaConverterJobStatusEventHandlerDefinition()
    {
        EndpointName = "mediaconverter-video-processed";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, 
        IConsumerConfigurator<SQSMediaConverterJobStatusEventHandler> consumerConfigurator, 
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}
