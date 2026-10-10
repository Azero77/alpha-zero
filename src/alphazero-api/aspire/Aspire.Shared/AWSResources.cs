namespace Aspire.Shared
{
    public class AWSResources
    {
        public const string Section = "AWS:Resources";
        public S3Settings? VideosInputS3 { get; set; }
        public S3Settings? CdnS3 { get; set; }
        public S3Settings? DocumentInputS3Public { get; set; }
        public S3Settings? DocumentInputS3Private { get; set; }
        public string? VideoCdnDomain { get; set; }
        public string? DocumentPublicCdnDomain { get; set; }
        public string? DocumentPrivateCdnDomain { get; set; }
        public SQSQueueSettings? VideoPublishedQueue { get; set; }
        public SQSQueueSettings? VideoFailedQueue { get; set; }
        public SQSQueueSettings? VideoProgressQueue { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedQueue { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedEvent { get; set; }
        public SQSQueueSettings? DocumentUploadedQueue { get; set; }
        public SQSQueueSettings? DocumentProcessingCompletedQueue { get; set; }
        public SQSQueueSettings? DocumentProcessingFailedQueue { get; set; }
        public string StepFunctionArn { get; set; } = string.Empty;
        public string MediaConvertRoleArn { get; set; } = string.Empty;
        public string MediaConvertKeyKMSArn { get; set; } = string.Empty;
        public string MasterClearKeyParameter { get; set; } = "/AlphaZero/VideoPipeline/MasterClearKey";
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
public sealed class R2Options
{
    public const string SectionName = "R2Options";

    public string AccessKeyId { get; init; } = string.Empty;

    public string SecretAccessKey { get; init; } = string.Empty;

    public string PublicBucketName { get; init; } = string.Empty;

    public string PrivateBucketName { get; init; } = string.Empty;

    public string VideosBucketName { get; init; } = string.Empty;

    public string ServiceURL { get; init; } = string.Empty;
}