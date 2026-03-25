using MassTransit;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Sagas;

internal class VideoUploadingSaga: MassTransitStateMachine<VideoState>
{
}
public class VideoState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public VideoStates CurrentState {get;set;}
}



public enum VideoStates
{
    Pending,//Video is uploading
    Processing, //Video is being transcoded
    Publishing, //Video has being transcoded and we need to add it to the courses
    Published,
    Failed
}
