using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using Amazon.SQS;
using Amazon.SQS.Model;
using Aspire.Shared;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Infrastructure.Workers;

public class S3VideoUploadedSQSPoller : BackgroundService
{
    private readonly VideoUploadedSQSQueueSettings sqsSettings;
    private readonly IPublishEndpoint _publisher;
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<S3VideoUploadedSQSPoller> _logger;
    public S3VideoUploadedSQSPoller(AWSResources resources, IAmazonSQS sqs, IPublishEndpoint publisher, ILogger<S3VideoUploadedSQSPoller> logger)
    {
        this.sqsSettings = resources.VideoUploadedQueue ?? throw new ArgumentException();
        this._sqs = sqs;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            //To Implement Later, We have to implement Idompotency to insure the video is handled once
            var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = sqsSettings.QueueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20
            });
            if (response.Messages == null || response.Messages.Count == 0)
            {
                // No messages, just wait a bit and continue
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }


            foreach (var msg in response.Messages)
            {
                var s3Event = Amazon.S3.Util.S3EventNotification.ParseJson(msg.Body);

                foreach (var record in s3Event.Records)
                {
                    _logger.LogInformation("Video uploaded: {record}", record.S3.Object.Key);
                    await _publisher.Publish<VideoUploadedEvent>(new VideoUploadedEvent(record.S3.Object.Key, record.S3.Bucket.Name));
                }

                await _sqs.DeleteMessageAsync(sqsSettings.QueueUrl, msg.ReceiptHandle);
            }
        }
    }
}
