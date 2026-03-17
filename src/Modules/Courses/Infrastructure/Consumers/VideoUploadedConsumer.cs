using Amazon.S3.Util;
using MassTransit;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public record VideoUploadedEvent(string Key, string Bucket);
public class VideoUploadedEventHandler : IConsumer<VideoUploadedEvent>
{
    public Task Consume(ConsumeContext<VideoUploadedEvent> context)
    {
        Console.WriteLine($"[MassTransit] Received video upload: {context.Message.Key} in bucket {context.Message.Key}");
        return Task.CompletedTask;
    }
}