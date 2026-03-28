namespace Aspire.Shared
{
    public class AWSResources
    {
        public const string Section = "AWS:Resources";
        public S3Settings? InputS3 { get; set; }
        public S3Settings? OutputS3 { get; set; }
        public CloudFrontSettings? CloudFront { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedQueue { get; set; }
        public VideoUploadedSQSQueueSettings? VideoUploadedEvent { get; set; }
        public string MediaConvertRoleArn { get; set; } = string.Empty;
        public string MediaConvertKeyKMSArn { get; set; } = string.Empty;
    }
    public class S3Settings
    {
        public string BucketName { get; set; } = string.Empty;
    }
    public class CloudFrontSettings
    {
        public string DistributionDomain { get; set; } = string.Empty;
        public string PublicKeyId { get; set; } = string.Empty;
        public string PrivateKeySecretName { get; set; } = string.Empty; // Store the RSA private key in Secrets Manager
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
