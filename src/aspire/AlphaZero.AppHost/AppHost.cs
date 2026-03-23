
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda.Destinations;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Constructs;

var builder = DistributedApplication.CreateBuilder(args);


var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);


var awscdkStack = builder.AddAWSCDKStack("AlphaZero-Aspire")
    .WithReference(awsSdkConfig);


var mediaConvertRole = new Role((Construct)awscdkStack.Resource.Construct, "AspireMediaConvertServiceRole", new RoleProps()
{
    AssumedBy = new ServicePrincipal("mediaconvert.amazonaws.com"),
    Description = "Role for mediaconvert job to access s3",
    RoleName = "Aspire-Mediaconvert-Role"
});
mediaConvertRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSMediaConvertRole"));

var input_s3 = awscdkStack.AddS3Bucket("InputS3", new BucketProps
{
    Cors = new[]
    {
        new CorsRule
        {
            AllowedMethods = new[] { HttpMethods.GET},
            AllowedOrigins = new[] { "http://localhost:5173", "*" },
            AllowedHeaders = new[] { "content-type", "x-amz-meta-file-name", "*" },
            ExposedHeaders = new[] { "ETag", "x-amz-meta-file-name" },
            MaxAge = 3600
        }
    }
});
var videoUploadedSQSQueue = awscdkStack.AddSQSQueue("VideoUploadedQueue");
var sns = awscdkStack.AddSNSTopic("VideoUploadedEvent")
    .AddSubscription(videoUploadedSQSQueue, new SqsSubscriptionProps()
    {
        RawMessageDelivery = true
    });
input_s3.AddObjectCreatedNotification(sns);
var output_s3 = awscdkStack.AddS3Bucket("OutputS3");

input_s3.Resource.Construct.GrantRead(mediaConvertRole);
output_s3.Resource.Construct.GrantReadWrite(mediaConvertRole);
var api = builder.AddProject<Projects.AlphaZero_API>("alphazero-api")
    .WithReference(awsSdkConfig)
    .WithReference(input_s3)
    .WithReference(output_s3)
    .WithReference(videoUploadedSQSQueue)
    .WithEnvironment("AWS__Resources__MediaConvertRoleArn", mediaConvertRole.RoleArn)
    .WithEnvironment("AWS__Resources__MediaConvertKeyKMSArn", "arn:aws:kms:eu-north-1:555106000478:key/81cf0007-4fce-4644-909f-7b8c7b1dc45e"); //managed outside the cdk, temporary for testing 


builder.Build().Run();
