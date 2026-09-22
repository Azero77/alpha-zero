using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using AlphaZero.Cdk.Constructs;
using Aspire.Hosting;
using Constructs;
using AlphaZero.Cdk.Stacks;

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);

var awscdkStack = builder.AddAWSCDKStack("AlphaZero")
    .WithReference(awsSdkConfig);

var stackConstruct = (Construct)awscdkStack.Resource.Construct;

#region aws

var storageStack = new StorageStack(stackConstruct, "Storage");
// 4. Shared Step Functions Video Pipeline (from AlphaZero.Cdk)
var videoPipeline = new VideoPipelineConstruct(stackConstruct, "VideoPipeline", new VideoPipelineConstructProps
{

    InputBucket = storageStack.InputBucket,
    TransientBucket = storageStack.TransientBucket,
    VideoPublishedQueue = storageStack.VideoPublishedQueue,
    VideoFailedQueue = storageStack.VideoFailedQueue,
    VideoProgressQueue = storageStack.VideoProgressQueue,
    MediaConvertRole = storageStack.MediaConvertRole,
});

// Expose CDK constructs as Aspire resources for automatic service discovery and configuration binding
var inputS3 = awscdkStack.AddConstruct("InputS3", _ => (Bucket)storageStack.InputBucket);
var transientS3 = awscdkStack.AddConstruct("TransientS3", _ => (Bucket)storageStack.TransientBucket);
var outputS3 = awscdkStack.AddConstruct("OutputS3", _ => (Bucket)storageStack.TransientBucket);
var videoPublishedQueue = awscdkStack.AddConstruct("VideoPublishedQueue", _ => (Queue)storageStack.VideoPublishedQueue);
var videoFailedQueue = awscdkStack.AddConstruct("VideoFailedQueue", _ => (Queue)storageStack.VideoFailedQueue);
var videoProgressQueue = awscdkStack.AddConstruct("VideoProgressQueue", _ => (Queue)storageStack.VideoProgressQueue);

new Amazon.CDK.CfnOutput(stackConstruct, "MediaConvertRoleArnOutput", new Amazon.CDK.CfnOutputProps
{
    Value = storageStack.MediaConvertRole.RoleArn
});

new Amazon.CDK.CfnOutput(stackConstruct, "StepFunctionArnOutput", new Amazon.CDK.CfnOutputProps
{
    Value = videoPipeline.PipelineStateMachine.StateMachineArn
});

#endregion
var cdnDomain = builder.Configuration["CdnDomain"] ?? "" ;

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
    .WithReference(inputS3)
    .WithReference(transientS3)
    .WithReference(outputS3)
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
    .WithEnvironment("AWS__Resources__MediaConvertKeyKMSArn", storageStack.MediaConvertKmsKeyArn)
    .WithEnvironment("AWS__Resources__CdnDomain", cdnDomain)
    .WithEnvironment("AWS__Resources__StepFunctionArn", awscdkStack.GetOutput("StepFunctionArnOutput"));

builder.Build().Run();
