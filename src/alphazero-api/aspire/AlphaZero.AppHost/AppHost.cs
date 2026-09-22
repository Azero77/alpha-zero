using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using AlphaZero.Cdk.Constructs;
using Aspire.Hosting;
using Constructs;

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);

var awscdkStack = builder.AddAWSCDKStack("AlphaZero")
    .WithReference(awsSdkConfig);

var stackConstruct = (Construct)awscdkStack.Resource.Construct;

#region aws

// 1. S3 Storage
var input_s3 = awscdkStack.AddS3Bucket("InputS3", new BucketProps
{
    BucketName = "alphazero-raw-uploads-dev",
    EventBridgeEnabled = true,
    Cors = new[]
    {
        new CorsRule
        {
            AllowedMethods = new[] { Amazon.CDK.AWS.S3.HttpMethods.GET, Amazon.CDK.AWS.S3.HttpMethods.PUT },
            AllowedOrigins = new[] { "*" },
            AllowedHeaders = new[] { "content-type", "x-amz-meta-file-name", "x-amz-meta-videoid", "x-amz-meta-tenantid", "x-amz-meta-title", "x-amz-meta-description", "x-amz-meta-videotranscodingmetehod", "x-amz-meta-videoencryptionmethod", "x-amz-meta-targetresourcearn", "x-amz-meta-isthumbnail", "*" },
            ExposedHeaders = new[] { "ETag", "x-amz-meta-file-name", "x-amz-meta-videoid", "x-amz-meta-tenantid", "x-amz-meta-title", "x-amz-meta-description", "x-amz-meta-videotranscodingmetehod", "x-amz-meta-videoencryptionmethod", "x-amz-meta-targetresourcearn", "x-amz-meta-isthumbnail" },
            MaxAge = 3600
        }
    }
});

var transient_s3 = awscdkStack.AddS3Bucket("TransientS3", new BucketProps
{
    BucketName = "alphazero-transient-processing-dev",
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

var output_s3 = awscdkStack.AddS3Bucket("OutputS3");

string cdnDomain = builder.Configuration["CdnDomain"] ?? "";
// var cdn_s3 = awscdkStack.AddS3Bucket("CdnS3", new BucketProps
// {
//     BucketName = string.IsNullOrEmpty(cdnDomain) ? "alphazero-cdn-dev" : cdnDomain,
//     PublicReadAccess = true,
//     BlockPublicAccess = new BlockPublicAccess(new BlockPublicAccessOptions
//     {
//         BlockPublicAcls = false,
//         IgnorePublicAcls = false,
//         BlockPublicPolicy = false,
//         RestrictPublicBuckets = false
//     }),
//     Cors = new[]
//     {
//         new CorsRule
//         {
//             AllowedMethods = new[] { Amazon.CDK.AWS.S3.HttpMethods.GET },
//             AllowedOrigins = new[] { "*" },
//             AllowedHeaders = new[] { "*" },
//             ExposedHeaders = new[] { "ETag" },
//             MaxAge = 3000
//         }
//     }
// });

// cdn_s3.Resource.Construct.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
// {
//     Actions = new[] { "s3:GetObject" },
//     Resources = new[] { $"{cdn_s3.Resource.Construct.BucketArn}/*" },
//     Principals = new[] { new AnyPrincipal() }
// }));

// 2. SQS Queues
var videoPublishedQueue = awscdkStack.AddSQSQueue("VideoPublishedQueue", new QueueProps
{
    QueueName = "VideoPublishedQueue"
});

var videoFailedQueue = awscdkStack.AddSQSQueue("VideoProcessingFailedQueue", new QueueProps
{
    QueueName = "VideoProcessingFailedQueue"
});

var videoProgressQueue = awscdkStack.AddSQSQueue("VideoProcessingProgressQueue", new QueueProps
{
    QueueName = "VideoProcessingProgressQueue"
});

// 3. MediaConvert Role
var mediaConvertRole = new Role(stackConstruct, "AspireMediaConvertServiceRole", new RoleProps
{
    AssumedBy = new ServicePrincipal("mediaconvert.amazonaws.com"),
    Description = "Role for mediaconvert job to access s3",
    RoleName = "Aspire-Mediaconvert-Role"
});
mediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
{
    Actions = new[] { "s3:GetObject", "s3:ListBucket", "s3:GetBucketLocation" },
    Resources = new[] { input_s3.Resource.Construct.BucketArn, $"{input_s3.Resource.Construct.BucketArn}/*" }
}));
mediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
{
    Actions = new[] { "s3:PutObject", "s3:GetObject", "s3:ListBucket", "s3:PutObjectAcl", "s3:AbortMultipartUpload", "s3:GetBucketLocation" },
    Resources = new[] { output_s3.Resource.Construct.BucketArn, $"{output_s3.Resource.Construct.BucketArn}/*" }
}));
string kmsArn = "arn:aws:kms:eu-north-1:555106000478:key/6dd21054-9423-469f-b94a-47a315d360fa";
mediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
{
    Actions = new[] { "kms:Decrypt", "kms:GenerateDataKey*", "kms:DescribeKey", "kms:CreateGrant" },
    Resources = new[] { kmsArn }
}));
new Amazon.CDK.CfnOutput(stackConstruct, "MediaConvertRoleArnOutput", new Amazon.CDK.CfnOutputProps
{
    Value = mediaConvertRole.RoleArn
});

// 4. Shared Step Functions Video Pipeline (from AlphaZero.Cdk)
var videoPipeline = new VideoPipelineConstruct(stackConstruct, "VideoPipeline", new VideoPipelineConstructProps
{
    InputBucket = input_s3.Resource.Construct,
    TransientBucket = transient_s3.Resource.Construct,
    VideoPublishedQueue = videoPublishedQueue.Resource.Construct,
    VideoFailedQueue = videoFailedQueue.Resource.Construct,
    VideoProgressQueue = videoProgressQueue.Resource.Construct,
    MediaConvertRole = mediaConvertRole
});

#endregion

var postgres = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis:16-3.5-alpine")
    .WithPgAdmin(cfg => cfg.WithImage("dpage/pgadmin4:snapshot"))
    .WithDataVolume(isReadOnly: false);
var db = postgres.AddDatabase("alphazerodb");

var resendApiKey = builder.Configuration["EmailSettings:ResendApiKey"] ?? "";
var googleClientId = builder.Configuration["Authentication:GoogleClientId"] ?? "";
var googleClientSecret = builder.Configuration["Authentication:GoogleClientSecret"] ?? "";

var keycloakDb = postgres.AddDatabase("idsrvDb");
var keyCloak = builder.AddKeycloakContainer("idsrv")
    .WithImport("KeycloakConfiguration.development.json")
    .WithPostgresDatabase(keycloakDb)
    .WithEnvironment("RESEND_API_KEY", resendApiKey)
    .WithEnvironment("GOOGLE_CLIENT_ID", googleClientId)
    .WithEnvironment("GOOGLE_CLIENT_SECRET", googleClientSecret);

var keycloakHttp = keyCloak.GetEndpoint("http");

var api = builder.AddProject<Projects.AlphaZero_API>("alphazero-api")
    .WithReference(awsSdkConfig)
    .WithReference(input_s3)
    .WithReference(transient_s3)
    .WithReference(output_s3)
    .WithReference(cdn_s3)
    .WithReference(videoPublishedQueue)
    .WithReference(videoFailedQueue)
    .WithReference(videoProgressQueue)
    .WithReference(db)
    .WaitFor(db)
    .WithReference(keyCloak)
    .WithEnvironment("Authentication__AuthorizationUrl", ReferenceExpression.Create($"{keycloakHttp}/realms/alpha-zero/protocol/openid-connect/auth"))
    .WithEnvironment("Authentication__TokenUrl", ReferenceExpression.Create($"{keycloakHttp}/realms/alpha-zero/protocol/openid-connect/token"))
    .WithEnvironment("Authentication__Authority", ReferenceExpression.Create($"{keycloakHttp}/realms/alpha-zero"))
    .WithEnvironment("AWS__Resources__MediaConvertRoleArn", awscdkStack.GetOutput("MediaConvertRoleArnOutput"))
    .WithEnvironment("AWS__Resources__MediaConvertKeyKMSArn", kmsArn)
    .WithEnvironment("AWS__Resources__CdnDomain", cdnDomain)
    .WithEnvironment("AWS__Resources__StepFunctionArn", videoPipeline.PipelineStateMachine.StateMachineArn);

builder.Build().Run();
