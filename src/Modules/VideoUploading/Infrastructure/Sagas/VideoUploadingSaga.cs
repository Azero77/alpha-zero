using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;
using static MassTransit.Logging.OperationName;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;

public class VideoUploadingSaga: MassTransitStateMachine<VideoState>
{
    public State Pending { get; private set; } = null!;
    public State Staged { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Publishing { get; private set; } = null!;
    public State Published { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<UploadVideoRequestedEvent> UploadVideoRequestedEvent { get; private set; } = null!;
    public Event<VideoDeliveredToInputEvent> VideoUploadedToInputEvent { get; private set; } = null!;
    public Event<VideoProcessingStartedEvent> VideoProcessingStartedEvent { get; private set; } = null!;
    public Event<VideoProcessingCompletedEvent> VideoProcessingCompletedEvent { get; private set; } = null!;
    public Event<VideoPublishedEvent> VideoPublishedEvent { get; private set; } = null!;
    public Event<VideoUploadFailedEvent> VideoUploadFailedEvent { get; private set; } = null!;
    public VideoUploadingSaga()
    {
        Event(() => UploadVideoRequestedEvent , e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoUploadedToInputEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoProcessingStartedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoProcessingCompletedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoPublishedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        Event(() => VideoUploadFailedEvent, e => e.CorrelateById(x => x.Message.VideoId));
        InstanceState(x => x.CurrentState);

        Initially(
            When(UploadVideoRequestedEvent)
            .TransitionTo(Pending));
        During(Pending,
         When(VideoUploadedToInputEvent)
            .If(context => !context.Saga.ProcessingStarted, x => StartProcessing(x))
            .Then(context => context.Saga.Key = context.Message.Key));
        During(Staged,
            When(VideoProcessingStartedEvent)
            .Then(context => context.Saga.ProcessingStarted = true)
            .TransitionTo(Processing));

        During(Processing,
            When(VideoProcessingCompletedEvent)
            .TransitionTo(Publishing));

        During(Publishing,
            When(VideoPublishedEvent)
            .TransitionTo(Published)
            .Finalize());
        During([Pending,Staged,Processing,Publishing] , When(VideoUploadFailedEvent)
            .Then(x => x.Saga.IsFailed = true)
            .TransitionTo(Failed));
    }
    private EventActivityBinder<VideoState, VideoDeliveredToInputEvent> StartProcessing(
    EventActivityBinder<VideoState, VideoDeliveredToInputEvent> binder)
    {
        return binder
            .Then(context => context.Saga.ProcessingStarted = true)
            .Publish(context => context.Init<StartVideoProcessingCommand>(
                new StartVideoProcessingCommand(
                    context.Message.Key,
                    context.Message.BucketName,
                    context.Message.VideoId)))
            .TransitionTo(Staged);
    }

}
public class VideoState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public string? MediaConverterJobId { get; set; }
    public string? Key { get; set; }
    public bool ProcessingStarted { get; set; } = false;
    public bool IsFailed { get; set; } = false;
    public int Version { get; set; }
}