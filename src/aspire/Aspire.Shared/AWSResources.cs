namespace Aspire.Shared
{
    public class AWSResources
    {
        public const string Section = "AWS:Resources";
        public S3Settings? InputS3 { get; set; }
        public S3Settings? OutputS3 { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedQueue { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedEvent { get; set; }
        
    }
    public class S3Settings
    {
        public string BucketName { get; set; } = string.Empty;
    }
    public class VideoUploadedSQSQueueSettings
    {
        public string QueueUrl { get; set; } = string.Empty;
    }

    public class VideoUploadedSNSTopicSettings
    {
        public string TopicArn { get; set; } = string.Empty;
    }
}
