using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
/// Separate class for listening to jobs and fire the events
/// </summary>
public class SQSMediaConverterJobCompletedEventHandler : 
    IConsumer<MediaConvertJobEvent>
{
    private readonly IModuleBus _moduleBus;
    private readonly ILogger<SQSMediaConverterJobCompletedEventHandler> _logger;

    public SQSMediaConverterJobCompletedEventHandler(IModuleBus moduleBus, ILogger<SQSMediaConverterJobCompletedEventHandler> logger)
    {
        _moduleBus = moduleBus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MediaConvertJobEvent> context)
    {
        var detail = context.Message.Detail;
        
        if (detail.Status != "COMPLETE")
        {
            _logger.LogWarning("Received MediaConvert event with status {Status} for Job {JobId}", detail.Status, detail.JobId);
            return;
        }

        if (detail.UserMetadata.TryGetValue(S3UploadService.VideoIdMetaDataHeader, out var videoIdStr) && 
            Guid.TryParse(videoIdStr, out var videoId))
        {
            _logger.LogInformation("MediaConvert Job {JobId} completed for Video {VideoId}", detail.JobId, videoId);
            await _moduleBus.Publish(new VideoProcessingCompletedEvent(videoId));
        }
        else
        {
            _logger.LogError("MediaConvert Job {JobId} completed but VideoId not found in metadata", detail.JobId);
        }
    }
}

public class SQSMediaConverterJobCompletedEventHandlerDefinition :
    ConsumerDefinition<SQSMediaConverterJobCompletedEventHandler>
{
    public SQSMediaConverterJobCompletedEventHandlerDefinition()
    {
        EndpointName = "mediaconverter-video-processed";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, 
        IConsumerConfigurator<SQSMediaConverterJobCompletedEventHandler> consumerConfigurator, 
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.ClearSerialization();
        endpointConfigurator.UseNewtonsoftRawJsonSerializer(RawSerializerOptions.AnyMessageType);
    }
}

public class VideoProcessingCompletedEventHandler :
    IConsumer<VideoProcessingCompletedEvent>
{
    private readonly ILogger<VideoProcessingCompletedEventHandler> _logger;

    public VideoProcessingCompletedEventHandler(ILogger<VideoProcessingCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<VideoProcessingCompletedEvent> context)
    {
        _logger.LogInformation("Processing completion for Video {VideoId}", context.Message.VideoId);
        // This can be used for additional logic outside the saga if needed
        return Task.CompletedTask;
    }
}
