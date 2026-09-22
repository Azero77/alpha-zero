namespace Aspire.Shared
{
    public class AWSResources
    {
        public const string Section = "AWS:Resources";
        public S3Settings? InputS3 { get; set; }
        public S3Settings? TransientS3 { get; set; }
        public S3Settings? OutputS3 { get; set; }
        public S3Settings? CdnS3 { get; set; }
        public string? CdnDomain { get; set; }
        public SQSQueueSettings? VideoPublishedQueue { get; set; }
        public SQSQueueSettings? VideoFailedQueue { get; set; }
        public SQSQueueSettings? VideoProgressQueue { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedQueue { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedEvent { get; set; }
        public string StepFunctionArn { get; set; } = string.Empty;
        public string MediaConvertRoleArn { get; set; } = string.Empty;
        public string MediaConvertKeyKMSArn { get; set; } = string.Empty;
    }
    public class S3Settings
    {
        public string BucketName { get; set; } = string.Empty;
    }
    public class SQSQueueSettings
    {
        public string QueueUrl { get; set; } = string.Empty;
    }
    public class VideoUploadedSQSQueueSettings : SQSQueueSettings
    {
    }

    public class VideoUploadedSNSTopicSettings
    {
        public string TopicArn { get; set; } = string.Empty;
    }
}
