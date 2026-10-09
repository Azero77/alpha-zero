using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KMS;
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
    public IBucket VideoInputBucketPrivate { get; }
    public IBucket DocumentInputBucketPrivate { get; }
    public IBucket DocumentInputBucketPublic { get; }
    public IBucket VideoTransientBucket { get; }
    public IQueue VideoPublishedQueue { get; }
    public IQueue VideoFailedQueue { get; }
    public IQueue VideoProgressQueue { get; }
    public IQueue DocumentProcessingCompletedQueue { get; }
    public IQueue DocumentProcessingFaultedQueue { get; }
    public IQueue DocumentUploadedQueue { get; }
    public IStringParameter MasterClearKey { get; }
    public IStringParameter VideoR2Credentials { get; }
    public IStringParameter DocumentR2Credentials { get; }
    public Role MediaConvertRole {get;}
    public string MediaConvertKmsKeyArn {get;}

    public StorageStack(Construct scope, string id, StorageStackProps? props = null) : base(scope, id, props)
    {
        var env = props?.Environment ?? "dev";

        // Input bucket for uploads with EventBridge notifications enabled
        VideoInputBucketPrivate = new Bucket(this, "RawUploadsBucket", new BucketProps
        {
            BucketName = $"alphazero-raw-uploads-{env}",
            EventBridgeEnabled = true,
            Cors =
            [
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" },
                    MaxAge = 3600
                }
            ]
        });

        // Document input buckets
        DocumentInputBucketPrivate = new Bucket(this, "DocumentInputBucketPrivate", new BucketProps
        {
            BucketName = $"alphazero-docs-in-private-{env}",
            EventBridgeEnabled = true,
            Cors =
            [
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" },
                    MaxAge = 3600
                }
            ]
        });

        DocumentInputBucketPublic = new Bucket(this, "DocumentInputBucketPublic", new BucketProps
        {
            BucketName = $"alphazero-docs-in-public-{env}",
            EventBridgeEnabled = true,
            Cors =
            [
                new CorsRule
                {
                    AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" },
                    MaxAge = 3600
                }
            ]
        });

        // Transient processing bucket with 24-hour lifecycle expiration rule
        VideoTransientBucket = new Bucket(this, "TransientProcessingBucket", new BucketProps
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

        DocumentUploadedQueue = new Queue(this, "DocumentUploadedQueue", new QueueProps
        {
            QueueName = $"DocumentUploadedQueue-{env}"
        });

        DocumentProcessingCompletedQueue = new Queue(this, "DocumentProcessingCompletedQueue", new QueueProps
        {
            QueueName = $"DocumentProcessingCompletedQueue-{env}"
        });

        DocumentProcessingFaultedQueue = new Queue(this, "DocumentProcessingFaultedQueue", new QueueProps
        {
            QueueName = $"DocumentProcessingFaultedQueue-{env}"
        });

        // SSM Parameters (SecureString references)
        MasterClearKey = StringParameter.FromSecureStringParameterAttributes(this, "MasterClearKey", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/VideoPipeline/MasterClearKey"
        });

        VideoR2Credentials = StringParameter.FromSecureStringParameterAttributes(this, "VideoR2Credentials", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/R2Credentials"
        });

        // CI/CD Note: The following SSM Parameter must be seeded with a JSON payload containing the Cloudflare
        // Access Key, Secret Key, PublicBucketName, and PrivateBucketName before deploying this CDK stack.
        // e.g., via GitHub Actions: aws ssm put-parameter --name "/AlphaZero/R2Credentials" --value '{"AccessKey":"...","SecretKey":"...","PublicBucketName":"...","PrivateBucketName":"..."}' --type "SecureString" --overwrite
        DocumentR2Credentials = StringParameter.FromSecureStringParameterAttributes(this, "DocumentR2Credentials", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/R2Credentials"
        });

// 3. MediaConvert Role
        MediaConvertRole = new Role(this, "AspireMediaConvertServiceRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("mediaconvert.amazonaws.com"),
            Description = "Role for mediaconvert job to access s3",
            RoleName = "Aspire-Mediaconvert-Role"
        });
        MediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "s3:GetObject", "s3:ListBucket", "s3:GetBucketLocation" },
            Resources = new[] { VideoInputBucketPrivate.BucketArn, $"{VideoInputBucketPrivate.BucketArn}/*" }
        }));
        MediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "s3:PutObject", "s3:GetObject", "s3:ListBucket", "s3:PutObjectAcl", "s3:AbortMultipartUpload", "s3:GetBucketLocation" },
            Resources = new[] { VideoTransientBucket.BucketArn, $"{VideoTransientBucket.BucketArn}/*" }
        }));
        var kms = new Key(this, "MediaConvertKMS", new KeyProps
        {
            // Optional, but useful for production
            Description = "KMS key used to encrypt MediaConvert output objects in S3",
            EnableKeyRotation = true
        });

        var mediaConvertKmsPolicy = new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "kms:Decrypt",
                "kms:GenerateDataKey*",
                "kms:DescribeKey",
                "kms:CreateGrant"
            },
            Resources = new[]
            {
                kms.KeyArn
            }
        });
        MediaConvertKmsKeyArn = kms.KeyArn;
        MediaConvertRole.AddToPolicy(mediaConvertKmsPolicy);

        // Outputs for Aspire AppHost
        new CfnOutput(this, "InputS3BucketNamePrivate", new CfnOutputProps { Value = VideoInputBucketPrivate.BucketName });
        new CfnOutput(this, "DocumentInputS3BucketNamePrivate", new CfnOutputProps { Value = DocumentInputBucketPrivate.BucketName });
        new CfnOutput(this, "DocumentInputS3BucketNamePublic", new CfnOutputProps { Value = DocumentInputBucketPublic.BucketName });
        new CfnOutput(this, "TransientS3BucketName", new CfnOutputProps { Value = VideoTransientBucket.BucketName });
        new CfnOutput(this, "VideoPublishedQueueUrl", new CfnOutputProps { Value = VideoPublishedQueue.QueueUrl });
        new CfnOutput(this, "VideoFailedQueueUrl", new CfnOutputProps { Value = VideoFailedQueue.QueueUrl });
        new CfnOutput(this, "VideoProgressQueueUrl", new CfnOutputProps { Value = VideoProgressQueue.QueueUrl });
        new CfnOutput(this, "DocumentUploadedQueueUrl", new CfnOutputProps { Value = DocumentUploadedQueue.QueueUrl });
        new CfnOutput(this, "DocumentProcessingCompletedQueueUrl", new CfnOutputProps { Value = DocumentProcessingCompletedQueue.QueueUrl });
        new CfnOutput(this, "DocumentProcessingFaultedQueueUrl", new CfnOutputProps { Value = DocumentProcessingFaultedQueue.QueueUrl });
        new CfnOutput(this, "MediaConvertRoleArnOutput", new CfnOutputProps { Value = MediaConvertRole.RoleArn });
        new CfnOutput(this, "MediaConvertKeyKMSArnOutput", new CfnOutputProps { Value = MediaConvertKmsKeyArn });
    }
}
