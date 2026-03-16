
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda.Destinations;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.SNS;

var builder = DistributedApplication.CreateBuilder(args);


var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);


var awscdkStack = builder.AddAWSCDKStack("AlphaZero-Aspire")
    .WithReference(awsSdkConfig);

var videoUploadedSQSQueue = awscdkStack.AddSQSQueue("VideoUploadedQueue");
var s3 = awscdkStack.AddS3Bucket("s3");
s3.AddEventNotification(
    videoUploadedSQSQueue,
    EventType.OBJECT_CREATED_PUT);

var api = builder.AddProject<Projects.AlphaZero_API>("alphazero-api")
    .WithReference(awsSdkConfig)
    .WithReference(s3)
    .WithReference(videoUploadedSQSQueue);


builder.Build().Run();
