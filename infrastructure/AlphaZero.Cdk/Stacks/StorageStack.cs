using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Constructs;

namespace AlphaZero.Cdk.Stacks;

public class StorageStackProps : StackProps
{
    public string Environment { get; set; } = "dev";
}

public class StorageStack : Stack
{
    public IBucket InputBucket { get; }
    public IBucket TransientBucket { get; }
    public IQueue VideoPublishedQueue { get; }
    public IQueue VideoFailedQueue { get; }
    public IQueue VideoProgressQueue { get; }
    public IStringParameter MasterClearKey { get; }
    public IStringParameter R2Credentials { get; }

    public StorageStack(Construct scope, string id, StorageStackProps? props = null) : base(scope, id, props)
    {
        var env = props?.Environment ?? "dev";

        // Input bucket for uploads with EventBridge notifications enabled
        InputBucket = new Bucket(this, "RawUploadsBucket", new BucketProps
        {
            BucketName = $"alphazero-raw-uploads-{env}",
            EventBridgeEnabled = true,
            Cors = new[]
            {
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" },
                    MaxAge = 3600
                }
            }
        });

        // Transient processing bucket with 24-hour lifecycle expiration rule
        TransientBucket = new Bucket(this, "TransientProcessingBucket", new BucketProps
        {
            BucketName = $"alphazero-transient-processing-{env}",
            LifecycleRules = new[]
            {
                new LifecycleRule
                {
                    Id = "ExpireTransientFilesAfter24Hours",
                    Enabled = true,
                    Expiration = Duration.Days(1)
                }
            }
        });

        // SQS Queues
        VideoPublishedQueue = new Queue(this, "VideoPublishedQueue", new QueueProps
        {
            QueueName = $"VideoPublishedQueue-{env}"
        });

        VideoFailedQueue = new Queue(this, "VideoProcessingFailedQueue", new QueueProps
        {
            QueueName = $"VideoProcessingFailedQueue-{env}"
        });

        VideoProgressQueue = new Queue(this, "VideoProcessingProgressQueue", new QueueProps
        {
            QueueName = $"VideoProcessingProgressQueue-{env}"
        });

        // SSM Parameters (SecureString references)
        MasterClearKey = StringParameter.FromSecureStringParameterAttributes(this, "MasterClearKey", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/VideoPipeline/MasterClearKey"
        });

        R2Credentials = StringParameter.FromSecureStringParameterAttributes(this, "R2Credentials", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/VideoPipeline/R2Credentials"
        });
    }
}
