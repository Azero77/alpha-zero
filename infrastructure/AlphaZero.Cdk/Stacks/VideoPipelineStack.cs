using System.IO;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using EcsContainerDefinitionOptions = Amazon.CDK.AWS.ECS.ContainerDefinitionOptions;
using SfnContainerOverride = Amazon.CDK.AWS.StepFunctions.Tasks.ContainerOverride;
using SfnTaskEnvironmentVariable = Amazon.CDK.AWS.StepFunctions.Tasks.TaskEnvironmentVariable;

namespace AlphaZero.Cdk.Stacks;

public class VideoPipelineStackProps : StackProps
{
    public required StorageStack Storage { get; set; }
}

public class VideoPipelineStack : Stack
{
    public StateMachine PipelineStateMachine { get; }

    public VideoPipelineStack(Construct scope, string id, VideoPipelineStackProps props) : base(scope, id, props)
    {
        var storage = props.Storage;

        var repoRoot = FindRepoRoot();
        var analyzerPath = Path.Combine(repoRoot, "src/lambdas/AlphaZero.VideoAnalyzer");
        var jobPreparerPath = ResolveJobPreparerAsset(repoRoot);
        var r2MoverPath = Path.Combine(repoRoot, "src/workers/AlphaZero.R2Mover");

        // 1. Lambda Functions
        var analyzerFn = new DockerImageFunction(this, "VideoAnalyzerFunction", new DockerImageFunctionProps
        {
            FunctionName = "alphazero-video-analyzer",
            Code = DockerImageCode.FromImageAsset(analyzerPath),
            Timeout = Duration.Minutes(2),
            MemorySize = 512
        });
        storage.InputBucket.GrantRead(analyzerFn);

        var jobPreparerFn = new Function(this, "JobPreparerFunction", new FunctionProps
        {
            FunctionName = "alphazero-job-preparer",
            Runtime = Runtime.PROVIDED_AL2023,
            Handler = "bootstrap",
            Code = Code.FromAsset(jobPreparerPath),
            Timeout = Duration.Seconds(30),
            MemorySize = 256
        });
        storage.InputBucket.GrantReadWrite(jobPreparerFn);
        storage.MasterClearKey.GrantRead(jobPreparerFn);

        // 2. ECS Fargate Tasks
        IVpc vpc;
        var account = this.Account;
        var region = this.Region;
        var hasConcreteEnv = !string.IsNullOrEmpty(account) && !account.Contains("Token") &&
                             !string.IsNullOrEmpty(region) && !region.Contains("Token");

        if (hasConcreteEnv)
        {
            try
            {
                vpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions { IsDefault = true });
            }
            catch
            {
                vpc = new Vpc(this, "TranscoderVpc", new VpcProps { MaxAzs = 2, NatGateways = 1 });
            }
        }
        else
        {
            vpc = new Vpc(this, "TranscoderVpc", new VpcProps { MaxAzs = 2, NatGateways = 1 });
        }

        var cluster = new Cluster(this, "TranscoderCluster", new ClusterProps { Vpc = vpc });

        var fargateTranscoderTaskDef = new FargateTaskDefinition(this, "TranscoderTaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 2048,
            MemoryLimitMiB = 4096
        });
        fargateTranscoderTaskDef.AddContainer("TranscoderContainer", new EcsContainerDefinitionOptions
        {
            Image = ContainerImage.FromRegistry("ghcr.io/azero77/ffmpeg-hls-transcoder:latest"),
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps { StreamPrefix = "Transcoder" })
        });
        storage.InputBucket.GrantRead(fargateTranscoderTaskDef.TaskRole);
        storage.TransientBucket.GrantWrite(fargateTranscoderTaskDef.TaskRole);

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
        storage.TransientBucket.GrantRead(fargateR2MoverTaskDef.TaskRole);
        storage.R2Credentials.GrantRead(fargateR2MoverTaskDef.TaskRole);

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
            Queue = storage.VideoFailedQueue,
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
            LambdaFunction = analyzerFn,
            OutputPath = "$.Payload"
        });
        analyzeTask.AddRetry(transientRetry);
        analyzeTask.AddCatch(failNotificationTask);

        var prepareJobTask = new LambdaInvoke(this, "PrepareJobTask", new LambdaInvokeProps
        {
            LambdaFunction = jobPreparerFn,
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

        var mediaConvertTask = new CustomState(this, "MediaConvertTask", new CustomStateProps
        {
            StateJson = new Dictionary<string, object>
            {
                { "Type", "Task" },
                { "Resource", "arn:aws:states:::mediaconvert:createJob.sync" },
                { "Parameters", new Dictionary<string, object>
                    {
                        { "Role", "arn:aws:iam::ACCOUNT_ID:role/MediaConvertRole" },
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
            Queue = storage.VideoPublishedQueue,
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
                    { "bucket", new Dictionary<string, object> { { "name", new[] { storage.InputBucket.BucketName } } } },
                    { "object", new Dictionary<string, object> { { "key", new[] { new Dictionary<string, object> { { "suffix", ".mp4" } } } } } }
                }
            }
        });

        s3EventRule.AddTarget(new SfnStateMachine(PipelineStateMachine));

        // 6. Observability & Alarms
        var alarmTopic = new Topic(this, "PipelineAlarmsTopic");
        new Alarm(this, "ExecutionsFailedAlarm", new AlarmProps
        {
            Metric = PipelineStateMachine.MetricFailed(),
            Threshold = 1,
            EvaluationPeriods = 1
        }).AddAlarmAction(new SnsAction(alarmTopic));

        new Alarm(this, "ExecutionsTimedOutAlarm", new AlarmProps
        {
            Metric = PipelineStateMachine.MetricTimedOut(),
            Threshold = 1,
            EvaluationPeriods = 1
        }).AddAlarmAction(new SnsAction(alarmTopic));

        new Alarm(this, "ExecutionTimeAlarm", new AlarmProps
        {
            Metric = PipelineStateMachine.MetricTime(),
            Threshold = Duration.Minutes(30).ToMilliseconds(),
            EvaluationPeriods = 1
        }).AddAlarmAction(new SnsAction(alarmTopic));
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
