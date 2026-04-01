
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda.Destinations;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Aspire.Hosting;
using Constructs;

var builder = DistributedApplication.CreateBuilder(args);


var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);


var awscdkStack = builder.AddAWSCDKStack("AlphaZero")
    .WithReference(awsSdkConfig);


var mediaConvertRole = new Role((Construct)awscdkStack.Resource.Construct, "AspireMediaConvertServiceRole", new RoleProps()
{
    AssumedBy = new ServicePrincipal("mediaconvert.amazonaws.com"),
    Description = "Role for mediaconvert job to access s3",
    RoleName = "Aspire-Mediaconvert-Role"
});

var input_s3 = awscdkStack.AddS3Bucket("InputS3", new BucketProps
{
    Cors = new[]
    {
        new CorsRule
        {
            AllowedMethods = new[] { HttpMethods.GET,HttpMethods.PUT},
            AllowedOrigins = new[] { "*" },
            AllowedHeaders = new[] { "content-type", "x-amz-meta-file-name", "x-amz-meta-videoid", "*" },
            ExposedHeaders = new[] { "ETag", "x-amz-meta-file-name", "x-amz-meta-videoid" },
            MaxAge = 3600
        }
    }
});
var videoUploadedSQSQueue = awscdkStack.AddSQSQueue("VideoUploadedQueue",new QueueProps()
{
    QueueName = "VideoUploadedQueue"
});
var sns = awscdkStack.AddSNSTopic("VideoUploadedEvent")
    .AddSubscription(videoUploadedSQSQueue, new SqsSubscriptionProps()
    {
        RawMessageDelivery = true
    });
input_s3.AddObjectCreatedNotification(sns);
var output_s3 = awscdkStack.AddS3Bucket("OutputS3");
var cdn_s3 = awscdkStack.AddS3Bucket("CdnS3", new BucketProps()
{
    PublicReadAccess = true,
    BlockPublicAccess = new BlockPublicAccess(new BlockPublicAccessOptions
    {
        BlockPublicAcls = false,
        IgnorePublicAcls = false,
        BlockPublicPolicy = false,
        RestrictPublicBuckets = false
    }),

    Cors = new[]
    {
        new CorsRule
        {
            AllowedMethods = [HttpMethods.GET],
            AllowedOrigins = ["*"], // OK for now (S3 is your CDN)
            AllowedHeaders = ["*"],
            ExposedHeaders = ["ETag"],
            MaxAge = 3000
        }
    },
});

cdn_s3.Resource.Construct
    .AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps()
    {
        Actions = new[] { "s3:GetObject" },
        Resources = new[] { cdn_s3.Resource.Construct.BucketArn },
        Principals = new[] { new AnyPrincipal() }
    }));

mediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps()
{
    Actions = new[] { "s3:GetObject", "s3:ListBucket", "s3:GetBucketLocation" },
    Resources = new[] {
        input_s3.Resource.Construct.BucketArn,
        $"{input_s3.Resource.Construct.BucketArn}/*"
        }
}));


mediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps()
{
    Actions = new[] { "s3:PutObject", "s3:GetObject", "s3:ListBucket", "s3:PutObjectAcl", "s3:AbortMultipartUpload", "s3:GetBucketLocation" },
    Resources = new[] {
        output_s3.Resource.Construct.BucketArn,
        $"{output_s3.Resource.Construct.BucketArn}/*"
        }
}));

string kmsArn = "arn:aws:kms:eu-north-1:555106000478:key/6dd21054-9423-469f-b94a-47a315d360fa";//managed outside the cdk, temporary for testing 
mediaConvertRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps()
{
    Actions = new[] { "kms:Decrypt", "kms:GenerateDataKey*", "kms:DescribeKey", "kms:CreateGrant" },
    Resources = new[] { kmsArn }
}));

new Amazon.CDK.CfnOutput((Construct)awscdkStack.Resource.Construct, "MediaConvertRoleArnOutput", new Amazon.CDK.CfnOutputProps
{
    Value = mediaConvertRole.RoleArn
});


var mediaConvertRule = new Rule((Construct)awscdkStack.Resource.Construct,"JobCompletedRule",new RuleProps()
{
    EventPattern = new EventPattern()
    {
        Source = ["aws.mediaconvert"],
        DetailType = ["MediaConvert Job State Change"],
        Detail = new Dictionary<string, object>()
        {
            {"status" , new string[] {"COMPLETE", "PROGRESSING", "ERROR" } }
        }
    }
});

var videoProcessedQueue = awscdkStack.AddSQSQueue("mediaconverter-video-processed", new QueueProps
{
    QueueName = "mediaconverter-video-processed"
});
videoProcessedQueue.Resource.Construct.GrantSendMessages(new ServicePrincipal("events.amazonaws.com"));
mediaConvertRule.AddTarget(new SqsQueue(videoProcessedQueue.Resource.Construct));

var postgres = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis:16-3.5-alpine")
    .WithPgAdmin(cfg => cfg.WithImage("dpage/pgadmin4:snapshot"))
    .WithDataVolume(isReadOnly: false);

var db = postgres.AddDatabase("alphazerodb");
var api = builder.AddProject<Projects.AlphaZero_API>("alphazero-api")
    .WithReference(awsSdkConfig)
    .WithReference(input_s3)
    .WithReference(output_s3)
    .WithReference(cdn_s3)
    .WithReference(videoUploadedSQSQueue)
    .WithReference(db)
    .WaitFor(db)
    .WithReference(videoProcessedQueue)
    .WithEnvironment("AWS__Resources__MediaConvertRoleArn", awscdkStack.GetOutput("MediaConvertRoleArnOutput"))
    .WithEnvironment("AWS__Resources__MediaConvertKeyKMSArn", kmsArn)
    ;
builder.Build().Run();
