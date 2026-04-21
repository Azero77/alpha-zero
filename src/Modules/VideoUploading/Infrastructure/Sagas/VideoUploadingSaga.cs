using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;

public class VideoUploadingSaga : MassTransitStateMachine<VideoState>
{
    // States
    public State Pending { get; private set; } = null!;           // Waiting for Upload
    public State Analyzing { get; private set; } = null!;         // FFProbe inspecting raw file
    public State Transcoding { get; private set; } = null!;       // MediaConvert optimizing the video
    public State Distributing { get; private set; } = null!;      // Moving S3 -> CDN
    public State Published { get; private set; } = null!;         // Final business state
    public State Failed { get; private set; } = null!;

    // Events
    public Event<UploadVideoRequestedEvent> UploadVideoRequestedEvent { get; private set; } = null!;
    public Event<VideoDeliveredToInputEvent> VideoDeliveredToInputEvent { get; private set; } = null!;
    public Event<VideoMetadataProcessedEvent> VideoMetadataProcessedEvent { get; private set; } = null!;
    public Event<VideoTranscodingStartedEvent> VideoTranscodingStartedEvent { get; private set; } = null!;
    public Event<VideoTranscodingFinishedEvent> VideoTranscodingFinishedEvent { get; private set; } = null!;
    public Event<VideoCdnSyncCompletedEvent> VideoCdnSyncCompletedEvent { get; private set; } = null!;
    public Event<VideoProcessingFailedEvent> VideoProcessingFailedEvent { get; private set; } = null!;

    public VideoUploadingSaga()
    {
        Event(() => UploadVideoRequestedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoDeliveredToInputEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoMetadataProcessedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoTranscodingStartedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoTranscodingFinishedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoCdnSyncCompletedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoProcessingFailedEvent, e => e.CorrelateById(x => x.Message.VideoId));

        InstanceState(x => x.CurrentState);
        SetCompletedWhenFinalized();
        Initially(
            When(UploadVideoRequestedEvent)
                .Then(context => {
                    context.Saga.TenantId = context.Message.TenantId;
                    context.Saga.EncryptionMethod = context.Message.EncryptionMethod;
                })
                .TransitionTo(Pending),
            
            When(VideoDeliveredToInputEvent)
                .Then(context => {
                    context.Saga.Key = context.Message.Key;
                    context.Saga.TenantId = context.Message.TenantId;
                })
                .Publish(context => new AnalyzeVideoCommand(
                    context.Message.VideoId, 
                    context.Message.Key))
                .TransitionTo(Analyzing));

        During(Pending,
            When(VideoDeliveredToInputEvent)
                .Then(context => {
                    context.Saga.Key = context.Message.Key;
                    context.Saga.TenantId = context.Message.TenantId;
                })
                .Publish(context => new AnalyzeVideoCommand(
                    context.Message.VideoId, 
                    context.Message.Key))
                .TransitionTo(Analyzing),
                
            When(VideoMetadataProcessedEvent)
                .Then(context => {
                    context.Saga.SourceWidth = context.Message.Width;
                    context.Saga.SourceHeight = context.Message.Height;
                    context.Saga.Duration = context.Message.Duration;
                })
                .Publish(context => new TranscodeVideoCommand(
                    context.Saga.CorrelationId,
                    context.Saga.Key!, 
                    context.Message.Width,
                    context.Message.Height,
                    context.Saga.EncryptionMethod))
                .TransitionTo(Transcoding));

        During(Analyzing,
            When(VideoMetadataProcessedEvent)
                .Then(context => {
                    context.Saga.SourceWidth = context.Message.Width;
                    context.Saga.SourceHeight = context.Message.Height;
                    context.Saga.Duration = context.Message.Duration;
                })
                .Publish(context => new TranscodeVideoCommand(
                    context.Saga.CorrelationId,
                    context.Saga.Key!, 
                    context.Message.Width,
                    context.Message.Height,
                    context.Saga.EncryptionMethod))
                .TransitionTo(Transcoding));

        During(Transcoding,
            When(VideoTranscodingStartedEvent)
                .Then(context => context.Saga.MediaConverterJobId = context.Message.JobId),
            
            When(VideoTranscodingFinishedEvent)
                .Then(context => {
                    context.Saga.S3OutputPrefix = context.Message.OutputKeyPrefix;
                })
                .Publish(context => new SyncVideoToCdnCommand(
                    context.Saga.CorrelationId, 
                    context.Saga.S3OutputPrefix!))
                .TransitionTo(Distributing));

        During(Distributing,
            When(VideoCdnSyncCompletedEvent)
                .Then(context => context.Saga.FinalUrl = context.Message.RelativeUrl)
                .Publish(context => new VideoPublishedEvent(context.Message.VideoId, context.Message.RelativeUrl))
                .TransitionTo(Published)
                .Finalize());

        DuringAny(
            When(VideoProcessingFailedEvent)
                .Then(x => x.Saga.IsFailed = true)
                .TransitionTo(Failed));
    }
}

public class VideoState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public Guid TenantId { get; set; }
    public string CurrentState { get; set; } = null!;
    public string? MediaConverterJobId { get; set; }
    public string? Key { get; set; }
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? S3OutputPrefix { get; set; }
    public string? FinalUrl { get; set; }
    public string? EncryptionMethod { get; set; }
    public bool IsFailed { get; set; } = false;
    public int Version { get; set; }
}
