using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using AlphaZero.Shared.Infrastructure.Repositores;
using MassTransit;

public class VideoUploadingStartedEventHandler(IRepository<CourseAsset> repository) : IConsumer<UploadVideoRequestedEvent>
{

    public Task Consume(ConsumeContext<UploadVideoRequestedEvent> context)
    {
        var videoAsset = VideoCourseAsset.Create();

        repository.Add(videoAsset);
    }

}


public class VideoUploadingFailedEventHandler : IConsumer<VideoProcessingFailedEvent>
{
    
}

public class VideoUploadedEventHandler : IConsumer<VideoPublishedEvent>
{
    
}
