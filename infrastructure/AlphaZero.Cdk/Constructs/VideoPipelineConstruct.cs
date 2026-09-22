using System;
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
using Constructs;
using EcsContainerDefinitionOptions = Amazon.CDK.AWS.ECS.ContainerDefinitionOptions;
using SfnContainerOverride = Amazon.CDK.AWS.StepFunctions.Tasks.ContainerOverride;
using SfnTaskEnvironmentVariable = Amazon.CDK.AWS.StepFunctions.Tasks.TaskEnvironmentVariable;

namespace AlphaZero.Cdk.Constructs;

public class VideoPipelineConstructProps
{
    public required IBucket InputBucket { get; set; }
    public required IBucket TransientBucket { get; set; }
    public required IQueue VideoPublishedQueue { get; set; }
    public required IQueue VideoFailedQueue { get; set; }
    public required IQueue VideoProgressQueue { get; set; }
    public IStringParameter? MasterClearKey { get; set; }
    public IStringParameter? R2Credentials { get; set; }
    public required IRole MediaConvertRole { get; set; }
    public IVpc? Vpc { get; set; }
}

public class VideoPipelineConstruct : Construct
{
    public StateMachine PipelineStateMachine { get; }
    public Topic AlarmTopic { get; }
    public DockerImageFunction VideoAnalyzerFunction { get; }
    public Function JobPreparerFunction { get; }

    public VideoPipelineConstruct(Construct scope, string id, VideoPipelineConstructProps props) : base(scope, id)
    {
        var repoRoot = FindRepoRoot();
        var analyzerPath = Path.Combine(repoRoot, "src/lambdas/AlphaZero.VideoAnalyzer");
        var jobPreparerPath = ResolveJobPreparerAsset(repoRoot);
        var r2MoverPath = Path.Combine(repoRoot, "src/workers/AlphaZero.R2Mover");

        // SSM Parameters
        var masterClearKey = props.MasterClearKey ?? StringParameter.FromSecureStringParameterAttributes(this, "MasterClearKey", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/VideoPipeline/MasterClearKey"
        });

        var r2Credentials = props.R2Credentials ?? StringParameter.FromSecureStringParameterAttributes(this, "R2Credentials", new SecureStringParameterAttributes
        {
            ParameterName = "/AlphaZero/VideoPipeline/R2Credentials"
        });

        // 1. Lambda Functions
        VideoAnalyzerFunction = new DockerImageFunction(this, "VideoAnalyzerFunction", new DockerImageFunctionProps
        {
            FunctionName = "alphazero-video-analyzer",
            Code = DockerImageCode.FromImageAsset(analyzerPath),
            Timeout = Duration.Minutes(2),
            MemorySize = 512
        });
        props.InputBucket.GrantRead(VideoAnalyzerFunction);

        JobPreparerFunction = new Function(this, "JobPreparerFunction", new FunctionProps
        {
            FunctionName = "alphazero-job-preparer",
            Runtime = Runtime.PROVIDED_AL2023,
            Handler = "bootstrap",
            Code = Code.FromAsset(jobPreparerPath),
            Timeout = Duration.Seconds(30),
            MemorySize = 256
        });
        props.InputBucket.GrantReadWrite(JobPreparerFunction);
        masterClearKey.GrantRead(JobPreparerFunction);

        // 2. ECS Fargate Tasks
        IVpc vpc = props.Vpc ?? ResolveVpc(scope);
        var cluster = new Cluster(this, "TranscoderCluster", new ClusterProps { Vpc = vpc });

        var fargateTranscoderTaskDef = new FargateTaskDefinition(this, "TranscoderTaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 2048,
            MemoryLimitMiB = 4096
        });
        fargateTranscoderTaskDef.AddContainer("TranscoderContainer", new EcsContainerDefinitionOptions
        {
            Image = ContainerImage.FromRegistry(VideoPipelineStackConfig.TranscoderDockerImage),
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps { StreamPrefix = "Transcoder" })
        });
        props.InputBucket.GrantRead(fargateTranscoderTaskDef.TaskRole);
        props.TransientBucket.GrantWrite(fargateTranscoderTaskDef.TaskRole);

        var fargateR2MoverTaskDef = new FargateTaskDefinition(this, "R2MoverTaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 512,
            MemoryLimitMiB = 1024
        });
        fargateR2MoverTaskDef.AddContainer("R2MoverContainer", new EcsContainerDefinitionOptions
        {
            Image = ContainerImage.FromAsset(r2MoverPath),
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps { StreamPrefix = "R2Mover" })
        });
        props.TransientBucket.GrantRead(fargateR2MoverTaskDef.TaskRole);
        r2Credentials.GrantRead(fargateR2MoverTaskDef.TaskRole);

        // 3. Retry Policies
        var transientRetry = new RetryProps
        {
            Errors = new[] { "TransientException", "Lambda.ServiceException", "Lambda.SdkClientException", "States.TaskFailed" },
            Interval = Duration.Seconds(2),
            MaxAttempts = 3,
            BackoffRate = 2.0
        };

        // 4. Step Functions Tasks & Failure Handling
        var failNotificationTask = new SqsSendMessage(this, "NotifyFailureTask", new SqsSendMessageProps
        {
            Queue = props.VideoFailedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["videoId"] = JsonPath.StringAt("$.videoId"),
                ["tenantId"] = JsonPath.StringAt("$.tenantId"),
                ["status"] = "Failed",
                ["error"] = JsonPath.StringAt("$.Cause")
            })
        }).Next(new Fail(this, "PipelineFailedState"));

        var analyzeTask = new LambdaInvoke(this, "AnalyzeVideoTask", new LambdaInvokeProps
        {
            LambdaFunction = VideoAnalyzerFunction,
            OutputPath = "$.Payload"
        });
        analyzeTask.AddRetry(transientRetry);
        analyzeTask.AddCatch(failNotificationTask);

        var prepareJobTask = new LambdaInvoke(this, "PrepareJobTask", new LambdaInvokeProps
        {
            LambdaFunction = JobPreparerFunction,
            OutputPath = "$.Payload"
        });
        prepareJobTask.AddRetry(transientRetry);
        prepareJobTask.AddCatch(failNotificationTask);

        var fargateTranscodeTask = new EcsRunTask(this, "RunFargateTranscoderTask", new EcsRunTaskProps
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

        var mediaConvertRoleArn = props.MediaConvertRole.RoleArn;
        var mediaConvertTask = new CustomState(this, "MediaConvertTask", new CustomStateProps
        {
            StateJson = new Dictionary<string, object>
            {
                { "Type", "Task" },
                { "Resource", "arn:aws:states:::mediaconvert:createJob.sync" },
                { "Parameters", new Dictionary<string, object>
                    {
                        { "Role", mediaConvertRoleArn },
                        { "Settings", JsonPath.StringAt("$.mediaConvertSettings") }
                    }
                }
            }
        });
        mediaConvertTask.AddRetry(transientRetry);
        mediaConvertTask.AddCatch(failNotificationTask);

        var transcodeChoice = new Choice(this, "EngineChoice")
            .When(Condition.StringEquals("$.transcodingEngine", "MediaConvert"), mediaConvertTask)
            .Otherwise(fargateTranscodeTask);

        var r2MoverTask = new EcsRunTask(this, "RunR2MoverTask", new EcsRunTaskProps
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

        var notifyPublishedTask = new SqsSendMessage(this, "NotifyPublishedTask", new SqsSendMessageProps
        {
            Queue = props.VideoPublishedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["videoId"] = JsonPath.StringAt("$.videoId"),
                ["tenantId"] = JsonPath.StringAt("$.tenantId"),
                ["status"] = "Published",
                ["playbackUrl"] = JsonPath.StringAt("$.r2Result.playbackUrl")
            })
        }).Next(new Succeed(this, "PipelineSucceededState"));

        var pipelineDefinition = analyzeTask
            .Next(prepareJobTask)
            .Next(transcodeChoice);

        fargateTranscodeTask.Next(r2MoverTask);
        mediaConvertTask.Next(r2MoverTask);
        r2MoverTask.Next(notifyPublishedTask);

        PipelineStateMachine = new StateMachine(this, "AlphaZeroVideoPipelineStateMachine", new StateMachineProps
        {
            StateMachineName = "AlphaZero-VideoPipeline",
            DefinitionBody = DefinitionBody.FromChainable(pipelineDefinition),
            Timeout = Duration.Minutes(45)
        });

        // 5. EventBridge Trigger from S3 (.mp4 files only)
        var s3EventRule = new Rule(this, "S3UploadRule", new RuleProps
        {
            EventPattern = new EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.InputBucket.BucketName } } } },
                    { "object", new Dictionary<string, object> { { "key", new[] { new Dictionary<string, object> { { "suffix", ".mp4" } } } } } }
                }
            }
        });
        s3EventRule.AddTarget(new SfnStateMachine(PipelineStateMachine));

        // 6. Observability & Alarms
        AlarmTopic = new Topic(this, "PipelineAlarmsTopic");
        new Alarm(this, "ExecutionsFailedAlarm", new AlarmProps
        {
            Metric = PipelineStateMachine.MetricFailed(),
            Threshold = 1,
            EvaluationPeriods = 1
        }).AddAlarmAction(new SnsAction(AlarmTopic));

        new Alarm(this, "ExecutionsTimedOutAlarm", new AlarmProps
        {
            Metric = PipelineStateMachine.MetricTimedOut(),
            Threshold = 1,
            EvaluationPeriods = 1
        }).AddAlarmAction(new SnsAction(AlarmTopic));

        new Alarm(this, "ExecutionTimeAlarm", new AlarmProps
        {
            Metric = PipelineStateMachine.MetricTime(),
            Threshold = Duration.Minutes(30).ToMilliseconds(),
            EvaluationPeriods = 1
        }).AddAlarmAction(new SnsAction(AlarmTopic));
    }

    private static IVpc ResolveVpc(Construct scope)
    {
        var stack = Stack.Of(scope);
        var account = stack.Account;
        var region = stack.Region;
        var hasConcreteEnv = !string.IsNullOrEmpty(account) && !account.Contains("Token") &&
                             !string.IsNullOrEmpty(region) && !region.Contains("Token");

        if (hasConcreteEnv)
        {
            try
            {
                return Vpc.FromLookup(scope, "DefaultVpc", new VpcLookupOptions { IsDefault = true });
            }
            catch
            {
                return new Vpc(scope, "TranscoderVpc", new VpcProps { MaxAzs = 2, NatGateways = 1 });
            }
        }

        return new Vpc(scope, "TranscoderVpc", new VpcProps { MaxAzs = 2, NatGateways = 1 });
    }

    private static string FindRepoRoot()
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

    private static string ResolveJobPreparerAsset(string repoRoot)
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
}
