using System.Collections.Generic;
using System.IO;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Aspire.Hosting;
using Constructs;
using EcsContainerDefinitionOptions = Amazon.CDK.AWS.ECS.ContainerDefinitionOptions;
using SfnContainerOverride = Amazon.CDK.AWS.StepFunctions.Tasks.ContainerOverride;
using SfnTaskEnvironmentVariable = Amazon.CDK.AWS.StepFunctions.Tasks.TaskEnvironmentVariable;

var builder = DistributedApplication.CreateBuilder(args);

var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);

var awscdkStack = builder.AddAWSCDKStack("AlphaZero")
    .WithReference(awsSdkConfig);

var stackConstruct = (Construct)awscdkStack.Resource.Construct;
var repoRoot = FindRepoRoot();
var analyzerPath = Path.Combine(repoRoot, "src/lambdas/AlphaZero.VideoAnalyzer");
var jobPreparerPath = ResolveJobPreparerAsset(repoRoot);
var r2MoverPath = Path.Combine(repoRoot, "src/workers/AlphaZero.R2Mover");

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
var cdn_s3 = awscdkStack.AddS3Bucket("CdnS3", new BucketProps
{
    BucketName = string.IsNullOrEmpty(cdnDomain) ? "alphazero-cdn-dev" : cdnDomain,
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
            AllowedMethods = new[] { Amazon.CDK.AWS.S3.HttpMethods.GET },
            AllowedOrigins = new[] { "*" },
            AllowedHeaders = new[] { "*" },
            ExposedHeaders = new[] { "ETag" },
            MaxAge = 3000
        }
    }
});

cdn_s3.Resource.Construct.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
{
    Actions = new[] { "s3:GetObject" },
    Resources = new[] { $"{cdn_s3.Resource.Construct.BucketArn}/*" },
    Principals = new[] { new AnyPrincipal() }
}));

// 2. SSM Parameters
var masterClearKey = StringParameter.FromSecureStringParameterAttributes(stackConstruct, "MasterClearKey", new SecureStringParameterAttributes
{
    ParameterName = "/AlphaZero/VideoPipeline/MasterClearKey"
});

var r2Credentials = StringParameter.FromSecureStringParameterAttributes(stackConstruct, "R2Credentials", new SecureStringParameterAttributes
{
    ParameterName = "/AlphaZero/VideoPipeline/R2Credentials"
});

// 3. SQS Queues
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

// 4. Lambdas
var analyzerFn = new DockerImageFunction(stackConstruct, "VideoAnalyzerFunction", new DockerImageFunctionProps
{
    FunctionName = "alphazero-video-analyzer",
    Code = DockerImageCode.FromImageAsset(analyzerPath),
    Timeout = Duration.Minutes(2),
    MemorySize = 512
});
input_s3.Resource.Construct.GrantRead(analyzerFn);

var jobPreparerFn = new Function(stackConstruct, "JobPreparerFunction", new FunctionProps
{
    FunctionName = "alphazero-job-preparer",
    Runtime = Runtime.PROVIDED_AL2023,
    Handler = "bootstrap",
    Code = Code.FromAsset(jobPreparerPath),
    Timeout = Duration.Seconds(30),
    MemorySize = 256
});
input_s3.Resource.Construct.GrantReadWrite(jobPreparerFn);
masterClearKey.GrantRead(jobPreparerFn);

// 5. ECS Fargate Definitions
var vpc = new Vpc(stackConstruct, "TranscoderVpc", new VpcProps
{
    MaxAzs = 2,
    NatGateways = 1
});
var cluster = new Cluster(stackConstruct, "TranscoderCluster", new ClusterProps { Vpc = vpc });

var fargateTranscoderTaskDef = new FargateTaskDefinition(stackConstruct, "TranscoderTaskDef", new FargateTaskDefinitionProps
{
    Cpu = 2048,
    MemoryLimitMiB = 4096
});
fargateTranscoderTaskDef.AddContainer("TranscoderContainer", new EcsContainerDefinitionOptions
{
    Image = ContainerImage.FromRegistry("ghcr.io/azero77/ffmpeg-hls-transcoder:latest"),
    Logging = LogDriver.AwsLogs(new AwsLogDriverProps { StreamPrefix = "Transcoder" })
});
input_s3.Resource.Construct.GrantRead(fargateTranscoderTaskDef.TaskRole);
transient_s3.Resource.Construct.GrantWrite(fargateTranscoderTaskDef.TaskRole);

var fargateR2MoverTaskDef = new FargateTaskDefinition(stackConstruct, "R2MoverTaskDef", new FargateTaskDefinitionProps
{
    Cpu = 512,
    MemoryLimitMiB = 1024
});
fargateR2MoverTaskDef.AddContainer("R2MoverContainer", new EcsContainerDefinitionOptions
{
    Image = ContainerImage.FromAsset(r2MoverPath),
    Logging = LogDriver.AwsLogs(new AwsLogDriverProps { StreamPrefix = "R2Mover" })
});
transient_s3.Resource.Construct.GrantRead(fargateR2MoverTaskDef.TaskRole);
r2Credentials.GrantRead(fargateR2MoverTaskDef.TaskRole);

// 6. MediaConvert Role & Legacy Support
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

// 7. Step Functions State Machine
var transientRetry = new RetryProps
{
    Errors = new[] { "TransientException", "Lambda.ServiceException", "Lambda.SdkClientException", "States.TaskFailed" },
    Interval = Duration.Seconds(2),
    MaxAttempts = 3,
    BackoffRate = 2.0
};

var failNotificationTask = new SqsSendMessage(stackConstruct, "NotifyFailureTask", new SqsSendMessageProps
{
    Queue = videoFailedQueue.Resource.Construct,
    MessageBody = TaskInput.FromObject(new Dictionary<string, object>
    {
        ["videoId"] = JsonPath.StringAt("$.videoId"),
        ["tenantId"] = JsonPath.StringAt("$.tenantId"),
        ["status"] = "Failed",
        ["error"] = JsonPath.StringAt("$.Cause")
    })
}).Next(new Fail(stackConstruct, "PipelineFailedState"));

var analyzeTask = new LambdaInvoke(stackConstruct, "AnalyzeVideoTask", new LambdaInvokeProps
{
    LambdaFunction = analyzerFn,
    OutputPath = "$.Payload"
});
analyzeTask.AddRetry(transientRetry);
analyzeTask.AddCatch(failNotificationTask);

var prepareJobTask = new LambdaInvoke(stackConstruct, "PrepareJobTask", new LambdaInvokeProps
{
    LambdaFunction = jobPreparerFn,
    OutputPath = "$.Payload"
});
prepareJobTask.AddRetry(transientRetry);
prepareJobTask.AddCatch(failNotificationTask);

var fargateTranscodeTask = new EcsRunTask(stackConstruct, "RunFargateTranscoderTask", new EcsRunTaskProps
{
    IntegrationPattern = IntegrationPattern.RUN_JOB,
    Cluster = cluster,
    TaskDefinition = fargateTranscoderTaskDef,
    LaunchTarget = new EcsFargateLaunchTarget(),
    ContainerOverrides = new[]
    {
        new SfnContainerOverride
        {
            ContainerDefinition = fargateTranscoderTaskDef.DefaultContainer!,
            Environment = new[]
            {
                new SfnTaskEnvironmentVariable { Name = "INPUT_FILE", Value = JsonPath.StringAt("$.sourceKey") },
                new SfnTaskEnvironmentVariable { Name = "JOB_CONFIG", Value = JsonPath.StringAt("$.jobConfigKey") }
            }
        }
    }
});
fargateTranscodeTask.AddRetry(transientRetry);
fargateTranscodeTask.AddCatch(failNotificationTask);

var mediaConvertTask = new CustomState(stackConstruct, "MediaConvertTask", new CustomStateProps
{
    StateJson = new Dictionary<string, object>
    {
        { "Type", "Task" },
        { "Resource", "arn:aws:states:::mediaconvert:createJob.sync" },
        { "Parameters", new Dictionary<string, object>
            {
                { "Role", mediaConvertRole.RoleArn },
                { "Settings", JsonPath.StringAt("$.mediaConvertSettings") }
            }
        }
    }
});
mediaConvertTask.AddRetry(transientRetry);
mediaConvertTask.AddCatch(failNotificationTask);

var transcodeChoice = new Choice(stackConstruct, "EngineChoice")
    .When(Condition.StringEquals("$.transcodingEngine", "MediaConvert"), mediaConvertTask)
    .Otherwise(fargateTranscodeTask);

var r2MoverTask = new EcsRunTask(stackConstruct, "RunR2MoverTask", new EcsRunTaskProps
{
    IntegrationPattern = IntegrationPattern.RUN_JOB,
    Cluster = cluster,
    TaskDefinition = fargateR2MoverTaskDef,
    LaunchTarget = new EcsFargateLaunchTarget(),
    ContainerOverrides = new[]
    {
        new SfnContainerOverride
        {
            ContainerDefinition = fargateR2MoverTaskDef.DefaultContainer!,
            Environment = new[]
            {
                new SfnTaskEnvironmentVariable { Name = "TENANT_ID", Value = JsonPath.StringAt("$.tenantId") },
                new SfnTaskEnvironmentVariable { Name = "VIDEO_ID", Value = JsonPath.StringAt("$.videoId") },
                new SfnTaskEnvironmentVariable { Name = "TRANSIENT_BUCKET", Value = JsonPath.StringAt("$.transientOutputBucket") }
            }
        }
    },
    ResultPath = "$.r2Result"
});
r2MoverTask.AddRetry(transientRetry);
r2MoverTask.AddCatch(failNotificationTask);

var notifyPublishedTask = new SqsSendMessage(stackConstruct, "NotifyPublishedTask", new SqsSendMessageProps
{
    Queue = videoPublishedQueue.Resource.Construct,
    MessageBody = TaskInput.FromObject(new Dictionary<string, object>
    {
        ["videoId"] = JsonPath.StringAt("$.videoId"),
        ["tenantId"] = JsonPath.StringAt("$.tenantId"),
        ["status"] = "Published",
        ["playbackUrl"] = JsonPath.StringAt("$.r2Result.playbackUrl")
    })
}).Next(new Succeed(stackConstruct, "PipelineSucceededState"));

var pipelineDefinition = analyzeTask
    .Next(prepareJobTask)
    .Next(transcodeChoice);

fargateTranscodeTask.Next(r2MoverTask);
mediaConvertTask.Next(r2MoverTask);
r2MoverTask.Next(notifyPublishedTask);

var stateMachine = new StateMachine(stackConstruct, "AlphaZeroVideoPipelineStateMachine", new StateMachineProps
{
    StateMachineName = "AlphaZero-VideoPipeline",
    DefinitionBody = DefinitionBody.FromChainable(pipelineDefinition),
    Timeout = Duration.Minutes(45)
});

// 8. EventBridge Trigger from S3 (.mp4 uploads)
var s3EventRule = new Rule(stackConstruct, "S3UploadRule", new RuleProps
{
    EventPattern = new EventPattern
    {
        Source = new[] { "aws.s3" },
        DetailType = new[] { "Object Created" },
        Detail = new Dictionary<string, object>
        {
            { "bucket", new Dictionary<string, object> { { "name", new[] { input_s3.Resource.Construct.BucketName } } } },
            { "object", new Dictionary<string, object> { { "key", new[] { new Dictionary<string, object> { { "suffix", ".mp4" } } } } } }
        }
    }
});

s3EventRule.AddTarget(new SfnStateMachine(stateMachine));

// 9. Observability & Alarms
var alarmTopic = new Topic(stackConstruct, "PipelineAlarmsTopic");
new Alarm(stackConstruct, "ExecutionsFailedAlarm", new AlarmProps
{
    Metric = stateMachine.MetricFailed(),
    Threshold = 1,
    EvaluationPeriods = 1
}).AddAlarmAction(new SnsAction(alarmTopic));

new Alarm(stackConstruct, "ExecutionsTimedOutAlarm", new AlarmProps
{
    Metric = stateMachine.MetricTimedOut(),
    Threshold = 1,
    EvaluationPeriods = 1
}).AddAlarmAction(new SnsAction(alarmTopic));

new Alarm(stackConstruct, "ExecutionTimeAlarm", new AlarmProps
{
    Metric = stateMachine.MetricTime(),
    Threshold = Duration.Minutes(30).ToMilliseconds(),
    EvaluationPeriods = 1
}).AddAlarmAction(new SnsAction(alarmTopic));

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
    .WithEnvironment("AWS__Resources__StepFunctionArn", stateMachine.StateMachineArn);

builder.Build().Run();

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "AlphaZero.sln")) ||
            Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}

static string ResolveJobPreparerAsset(string repoRoot)
{
    var candidates = new[]
    {
        Path.Combine(repoRoot, "src/lambdas/AlphaZero.JobPreparer/publish"),
        Path.Combine(repoRoot, "src/lambdas/AlphaZero.JobPreparer/src/AlphaZero.JobPreparer/bin/Release/net10.0"),
        Path.Combine(repoRoot, "src/lambdas/AlphaZero.JobPreparer/src/AlphaZero.JobPreparer/bin/Debug/net10.0"),
        Path.Combine(repoRoot, "src/lambdas/AlphaZero.JobPreparer/src/AlphaZero.JobPreparer")
    };

    foreach (var candidate in candidates)
    {
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    var defaultPublish = candidates[0];
    Directory.CreateDirectory(defaultPublish);
    return defaultPublish;
}
