using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class VideoUploadedConsumer : IConsumer<VideoUploadedEvent>
{
    public Task Consume(ConsumeContext<VideoUploadedEvent> context)
    {
        throw new NotImplementedException();
    }
}

public record VideoUploadedEvent(string Key,string Bucket);