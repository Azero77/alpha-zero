using Amazon.SQS;
using Amazon.SQS.Model;
using Aspire.Shared;
using Microsoft.Extensions.Hosting;

namespace AlphaZero.Modules.Courses.Infrastructure.Workers;

public class S3VideoUploadedSQSPoller : BackgroundService
{
    private readonly VideoUploadedSQSQueueSettings sqsSettings;
    private readonly IAmazonSQS _sqs;
    public S3VideoUploadedSQSPoller(AWSResources resources, IAmazonSQS sqs)
    {
        this.sqsSettings = resources.VideoUploadedQueue ?? throw new ArgumentException();
        this._sqs = sqs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
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
                    Console.WriteLine($"Video uploaded: {record.S3.Object.Key}");
                }

                await _sqs.DeleteMessageAsync(sqsSettings.QueueUrl, msg.ReceiptHandle);
            }
        }
    }
}
