using Aspire.Hosting;
var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);

var storageStackOutputs = builder.AddAWSCloudFormationStack("AlphaZeroStorageStack")
                                 .WithReference(awsSdkConfig);

var pipelineStackOutputs = builder.AddAWSCloudFormationStack("AlphaZeroVideoPipelineStack")
                                  .WithReference(awsSdkConfig);

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
    .WithReference(db)
    .WaitFor(db)
    .WithReference(keyCloak)
    .WithEnvironment("Authentication__AuthorizationUrl", ReferenceExpression.Create($"{keycloakHttp}/realms/alpha-zero/protocol/openid-connect/auth"))
    .WithEnvironment("Authentication__TokenUrl", ReferenceExpression.Create($"{keycloakHttp}/realms/alpha-zero/protocol/openid-connect/token"))
    .WithEnvironment("Authentication__Authority", ReferenceExpression.Create($"{keycloakHttp}/realms/alpha-zero"))
    .WithEnvironment("AWS__Resources__InputS3__BucketName", storageStackOutputs.GetOutput("InputS3BucketName"))
    .WithEnvironment("AWS__Resources__TransientS3__BucketName", storageStackOutputs.GetOutput("TransientS3BucketName"))
    .WithEnvironment("AWS__Resources__VideoPublishedQueue__QueueUrl", storageStackOutputs.GetOutput("VideoPublishedQueueUrl"))
    .WithEnvironment("AWS__Resources__VideoFailedQueue__QueueUrl", storageStackOutputs.GetOutput("VideoFailedQueueUrl"))
    .WithEnvironment("AWS__Resources__VideoProgressQueue__QueueUrl", storageStackOutputs.GetOutput("VideoProgressQueueUrl"))
    .WithEnvironment("AWS__Resources__MediaConvertRoleArn", storageStackOutputs.GetOutput("MediaConvertRoleArnOutput"))
    .WithEnvironment("AWS__Resources__MediaConvertKeyKMSArn", storageStackOutputs.GetOutput("MediaConvertKeyKMSArnOutput"))
    .WithEnvironment("AWS__Resources__StepFunctionArn", pipelineStackOutputs.GetOutput("StepFunctionArnOutput"))
    .WithEnvironment("AWS__Resources__CdnDomain", cdnDomain);

#pragma warning disable ASPIRE001
var videoAnalyzer = builder.AddAWSLambdaFunction<Projects.AlphaZero_VideoAnalyzer>("video-analyzer", "AlphaZero.VideoAnalyzer::AlphaZero.VideoAnalyzer.Function::FunctionHandler")
    .WithReference(awsSdkConfig)
    .WithEnvironment("AWS__Resources__InputS3__BucketName", storageStackOutputs.GetOutput("InputS3BucketName"));

var jobPreparer = builder.AddAWSLambdaFunction<Projects.AlphaZero_JobPreparer>("job-preparer", "AlphaZero.JobPreparer::AlphaZero.JobPreparer.Function::FunctionHandler")
    .WithReference(awsSdkConfig)
    .WithEnvironment("AWS__Resources__TransientS3__BucketName", storageStackOutputs.GetOutput("TransientS3BucketName"));

builder.Build().Run();
