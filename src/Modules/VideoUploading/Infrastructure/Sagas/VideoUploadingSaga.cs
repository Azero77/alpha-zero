using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using MassTransit;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;

internal class VideoUploadingSaga: MassTransitStateMachine<VideoState>
{
    public State Pending { get; private set; }
    public State Delivered { get; private set; }
    public State Processing { get; private set; }
    public State Publishing { get; private set; }
    public State Published { get; private set; }
    public State Failed{ get; private set; }

    public Event<UploadVideoRequestedEvent> UploadVideoRequestedEvent { get; private set; }
    public Event<VideoUploadedToInputEvent> VideoUploadedToInputEvent { get; private set; }
    public Event<VideoProcessingStartedEvent> VideoProcessingStartedEvent { get; private set; }
    public Event<VideoProcessingCompletedEvent> VideoProcessingCompletedEvent { get; private set; }
    public Event<VideoPublishedEvent> VideoPublishedEvent { get; private set; }
    public Event<VideoUploadFailedEvent> VideoUploadFailedEvent { get; private set; }
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
            .Then(context =>
            {
            })
            .TransitionTo(Pending));

        During(Pending,
            When(VideoUploadedToInputEvent)
                .TransitionTo(Delivered)
            );
    }
}
public class VideoState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public string? MediaConverterJobId { get; set; }
    public string? Key { get; set; }
}