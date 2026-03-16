using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class VideoUploadedConsumer : IConsumer<VideoUploadedEvent>
{
}

public record VideoUploadedEvent(string Key,string Bucket);