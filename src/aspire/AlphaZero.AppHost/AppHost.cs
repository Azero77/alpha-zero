
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda.Destinations;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;

var builder = DistributedApplication.CreateBuilder(args);


var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);


var awscdkStack = builder.AddAWSCDKStack("AlphaZero-Aspire")
    .WithReference(awsSdkConfig);

var s3 = awscdkStack.AddS3Bucket("s3", new BucketProps
{
    Cors = new[]
    {
        new CorsRule
        {
            AllowedMethods = new[] { HttpMethods.GET, HttpMethods.PUT, HttpMethods.POST, HttpMethods.DELETE, HttpMethods.HEAD },
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
s3.AddObjectCreatedNotification(sns);

var api = builder.AddProject<Projects.AlphaZero_API>("alphazero-api")
    .WithReference(awsSdkConfig)
    .WithReference(s3)
    .WithReference(videoUploadedSQSQueue);


builder.Build().Run();
