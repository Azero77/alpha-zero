using AWS.Messaging;
using Amazon.S3.Util;

namespace AlphaZero.Modules.Courses.Infrastructure.Consumers;

public class VideoUploadedEventHandler : IMessageHandler<S3EventNotification>
{

    public Task<MessageProcessStatus> HandleAsync(MessageEnvelope<S3EventNotification> messageEnvelope, CancellationToken token = default)
    {
        var s3Event = messageEnvelope.Message;

        foreach (var record in s3Event.Records)
        {
            var bucket = record.S3.Bucket.Name;
            var key = record.S3.Object.Key;

            // TODO: Process the uploaded video
            Console.WriteLine($"Received video upload: {key} in bucket {bucket}");
        }

        return Task.FromResult(MessageProcessStatus.Success());
    }
}