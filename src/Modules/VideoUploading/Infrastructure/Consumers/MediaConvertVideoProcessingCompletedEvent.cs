using AlphaZero.Modules.VideoUploading.Application.Commands.Complete;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.Infrastructure.Services;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Application;
using MassTransit;
using MediatR;
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
        
        if (detail.Status == "COMPLETE" && detail.UserMetadata.TryGetValue(S3UploadService.VideoIdMetaDataHeader, out var videoIdStr) &&
            Guid.TryParse(videoIdStr, out var videoId))
        {
            _logger.LogInformation("MediaConvert Job {JobId} completed for Video {VideoId}", detail.JobId, videoId);
            await _moduleBus.Publish(new VideoProcessingCompletedEvent(videoId));
            return;
        }
        else if (detail.Status == "PROGRESSING")
        {
            videoIdStr = detail.UserMetadata.GetValueOrDefault(S3UploadService.VideoIdMetaDataHeader);
            if (videoIdStr is null || !Guid.TryParse(videoIdStr, out videoId))
            {
                _logger.LogCritical("MediaConvert Job {JobId} Started for Video with no Id", detail.JobId);
                return;
            }
            if (
                !detail.UserMetadata.TryGetValue("sourceFile", out var inputPath) ||
                !TryGetParamsForInputPath(inputPath,out string key,out string bucket)
                )
            {
                _logger.LogCritical("MediaConvert Job {JobId} Started for Video with no InputPath", detail.JobId);
                await _moduleBus.Publish(new VideoUploadFailedEvent(videoId, string.Empty));
                return;

            }
            _logger.LogInformation("MediaConvert Job {JobId} Started for Video {VideoId}", detail.JobId, videoId);
            
            await _moduleBus.Publish(new VideoProcessingStartedEvent(key,bucket,videoId,detail.JobId));


        }
    }

    private bool TryGetParamsForInputPath(string? inputPath, out string key,out string bucket)
    {
        key = string.Empty;
        bucket = string.Empty;
        if (inputPath is null)
            return false;
        var regex = new Regex("^s3://([^/]+)/(.+)$");

        var match = regex.Match(inputPath);

        if (!match.Success) return false;
         key = match.Groups[2].Value;
        bucket= match.Groups[1].Value;

        return true ;
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
    private readonly IMediator _mediator;
    private readonly AppDbContext _dbContext;

    public VideoProcessingCompletedEventHandler(
        ILogger<VideoProcessingCompletedEventHandler> logger,
        IMediator mediator,
        AppDbContext dbContext)
    {
        _logger = logger;
        _mediator = mediator;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<VideoProcessingCompletedEvent> context)
    {
        var videoId = context.Message.VideoId;
        _logger.LogInformation("Infrastructure consumer triggered for Video {VideoId}", videoId);

        var videoState = await _dbContext.VideoState.FirstOrDefaultAsync(s => s.CorrelationId == videoId);
        if (videoState == null || videoState.Key == null)
        {
            _logger.LogError("VideoState not found for Video {VideoId}", videoId);
            return;
        }

        var result = await _mediator.Send(new CompleteVideoProcessingCommand(videoId, videoState.Key, videoState.TenantId));

        if (result.IsError)
        {
            _logger.LogError("Application command failed for Video {VideoId}: {Error}", videoId, result.FirstError.Description);
        }
    }
}