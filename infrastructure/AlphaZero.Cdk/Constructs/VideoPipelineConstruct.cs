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
    public Function S3VideoCreatedEventParserFunction { get; }
    public Function JobPreparerFunction { get; }

    public VideoPipelineConstruct(Construct scope, string id, VideoPipelineConstructProps props) : base(scope, id)
    {
        var repoRoot = FindRepoRoot();
        var analyzerDockerFilePath = "src/lambdas/AlphaZero.VideoAnalyzer/Dockerfile";
        var jobPreparerPath = "src/lambdas/AlphaZero.JobPreparer/publish";
        var r2MoverPath = Path.Combine(repoRoot, "src/workers/AlphaZero.R2Mover");
        // SSM Parameters
        var masterClearKey = props.MasterClearKey;

        var r2Credentials = props.R2Credentials;

        // 1. Lambda Functions

        S3VideoCreatedEventParserFunction = new Function(this, "S3VideoCreatedEventParserFunction",
            new FunctionProps()
            {
                FunctionName = "alphazero-video-uploaded-event-parser",
                Runtime = Runtime.PROVIDED_AL2023,
                Handler = "bootstrap",
                Code = Code.FromAsset("src/lambdas/AlphaZero.S3VideoCreatedEventParser/publish"),
                Timeout = Duration.Seconds(30),
                MemorySize = 256,
                Environment = new Dictionary<string, string>
                {
                    { "PROGRESS_QUEUE_URL", props.VideoProgressQueue.QueueUrl }
                }
            });

        props.InputBucket.GrantRead(S3VideoCreatedEventParserFunction);
        props.VideoProgressQueue.GrantSendMessages(S3VideoCreatedEventParserFunction);
        
        VideoAnalyzerFunction = new DockerImageFunction(this, "VideoAnalyzerFunction", new DockerImageFunctionProps
        {
            FunctionName = "alphazero-video-analyzer",
            Code = DockerImageCode.FromImageAsset(repoRoot, new AssetImageCodeProps 
            {
                File = analyzerDockerFilePath,
                Exclude = new[] { ".git", "cdk.out" },
                IgnoreMode = IgnoreMode.DOCKER
            }),
            Timeout = Duration.Minutes(2),
            MemorySize = 512,
            Environment = new Dictionary<string, string>
            {
                { "PROGRESS_QUEUE_URL", props.VideoProgressQueue.QueueUrl }
            }
        });
        props.InputBucket.GrantRead(VideoAnalyzerFunction);
        props.VideoProgressQueue.GrantSendMessages(VideoAnalyzerFunction);

        JobPreparerFunction = new Function(this, "JobPreparerFunction", new FunctionProps
        {
            FunctionName = "alphazero-job-preparer",
            Runtime = Runtime.PROVIDED_AL2023,
            Handler = "bootstrap",
            Code = Code.FromAsset(jobPreparerPath),
            Timeout = Duration.Seconds(30),
            MemorySize = 256,
            Environment = new Dictionary<string, string>
            {
                { "PROGRESS_QUEUE_URL", props.VideoProgressQueue.QueueUrl }
            }
        });
        props.InputBucket.GrantReadWrite(JobPreparerFunction);
        masterClearKey.GrantRead(JobPreparerFunction);
        props.VideoProgressQueue.GrantSendMessages(JobPreparerFunction);

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
        props.TransientBucket.GrantReadWrite(fargateTranscoderTaskDef.TaskRole);
        props.VideoProgressQueue.GrantSendMessages(fargateTranscoderTaskDef.TaskRole);

        var fargateR2MoverTaskDef = new FargateTaskDefinition(this, "R2MoverTaskDef", new FargateTaskDefinitionProps
        {
            Cpu = 512,
            MemoryLimitMiB = 1024
        });
        fargateR2MoverTaskDef.AddContainer("R2MoverContainer", new EcsContainerDefinitionOptions
        {
            Image = ContainerImage.FromAsset(repoRoot, new AssetImageProps
            {
                File = "src/workers/AlphaZero.R2Mover/Dockerfile",
                Exclude = new[] { ".git", "cdk.out" },
                IgnoreMode = IgnoreMode.DOCKER
            }),
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps { StreamPrefix = "R2Mover" })
        });
        props.TransientBucket.GrantRead(fargateR2MoverTaskDef.TaskRole);
        r2Credentials.GrantRead(fargateR2MoverTaskDef.TaskRole);
        props.VideoProgressQueue.GrantSendMessages(fargateR2MoverTaskDef.TaskRole);

        // 3. Retry Policies
        var transientRetry = new RetryProps
        {
            Errors = new[] { "TransientException", "Lambda.ServiceException", "Lambda.SdkClientException", "States.TaskFailed" },
            Interval = Duration.Seconds(2),
            MaxAttempts = 3,
            BackoffRate = 2.0
        };

        // 4. Step Functions Tasks & Failure Handling
        var catchProps = new CatchProps
        {
            ResultPath = "$.errorInfo"
        };

        var failNotificationTask = new SqsSendMessage(this, "NotifyFailureTask", new SqsSendMessageProps
        {
            Queue = props.VideoFailedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["VideoId"] = JsonPath.StringAt("$.VideoId"),
                ["TenantId"] = JsonPath.StringAt("$.TenantId"),
                ["Status"] = "Failed",
                ["Error"] = new Dictionary<string, object>
                {
                    ["errorType"] = JsonPath.StringAt("$.errorInfo.Error"),
                    ["cause"] = JsonPath.StringAt("$.errorInfo.Cause")
                },
                ["TargetResourceArn"] = JsonPath.StringAt("$.TargetResourceArn")
            })
        }).Next(new Fail(this, "PipelineFailedState"));

        var s3EVentParserTask = new LambdaInvoke(this, "S3EventParserTask", new LambdaInvokeProps()
        {
            LambdaFunction = S3VideoCreatedEventParserFunction,
            PayloadResponseOnly = true,
            Payload = TaskInput.FromObject(new Dictionary<string, object>()
            {
                ["BucketName"] = JsonPath.StringAt("$.detail.bucket.name"),
                ["SourceKey"] = JsonPath.StringAt( "$.detail.object.key")
            })
        });

        s3EVentParserTask.AddRetry(transientRetry);
        s3EVentParserTask.AddCatch(failNotificationTask, catchProps);
        var analyzeTask = new LambdaInvoke(this, "AnalyzeVideoTask", new LambdaInvokeProps
        {
            LambdaFunction = VideoAnalyzerFunction,
            PayloadResponseOnly = true,
            ResultPath = "$.SourceMetadata"
        });
        analyzeTask.AddRetry(transientRetry);
        analyzeTask.AddCatch(failNotificationTask, catchProps);

        var prepareJobTask = new LambdaInvoke(this, "PrepareJobTask", new LambdaInvokeProps
        {
            LambdaFunction = JobPreparerFunction,
            PayloadResponseOnly = true,
            ResultPath = "$.JobPrep"
        });
        prepareJobTask.AddRetry(transientRetry);
        prepareJobTask.AddCatch(failNotificationTask, catchProps);

        var fargateTranscodeTask = new EcsRunTask(this, "RunFargateTranscoderTask", new EcsRunTaskProps
        {
            IntegrationPattern = IntegrationPattern.RUN_JOB,
            Cluster = cluster,
            TaskDefinition = fargateTranscoderTaskDef,
            LaunchTarget = new EcsFargateLaunchTarget(),
            AssignPublicIp = true,
            Subnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
            ContainerOverrides = new[]
            {
                new SfnContainerOverride
                {
                    ContainerDefinition = fargateTranscoderTaskDef.DefaultContainer!,
                    Environment = new[]
                    {
                        new SfnTaskEnvironmentVariable { Name = "TRANSCODER__StorageProvider", Value = "S3" },
                        new SfnTaskEnvironmentVariable { Name = "TRANSCODER__S3__InputBucket", Value = props.InputBucket.BucketName },
                        new SfnTaskEnvironmentVariable { Name = "TRANSCODER__S3__OutputBucket", Value = props.TransientBucket.BucketName },
                        new SfnTaskEnvironmentVariable { Name = "TRANSCODER__INPUT_FILE", Value = JsonPath.StringAt("$.JobPrep.JobConfigKey") },
                        new SfnTaskEnvironmentVariable { Name = "TRANSCODER__ProgressQueueUrl", Value = props.VideoProgressQueue.QueueUrl },
                    }
                }
            },
            ResultPath = "$.transcoderResult"
        });
        fargateTranscodeTask.AddRetry(transientRetry);
        fargateTranscodeTask.AddCatch(failNotificationTask, catchProps);

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
                        { "Settings.$", "$.mediaConvertSettings" }
                    }
                }
            }
        });
        mediaConvertTask.AddRetry(transientRetry);
        mediaConvertTask.AddCatch(failNotificationTask, catchProps);

        var transcodeChoice = new Choice(this, "EngineChoice")
            .When(Condition.StringEquals("$.TranscodingEngine", "MediaConvert"), mediaConvertTask)
            .Otherwise(fargateTranscodeTask);

        var r2MoverTask = new EcsRunTask(this, "RunR2MoverTask", new EcsRunTaskProps
        {
            IntegrationPattern = IntegrationPattern.RUN_JOB,
            Cluster = cluster,
            TaskDefinition = fargateR2MoverTaskDef,
            LaunchTarget = new EcsFargateLaunchTarget(),
            AssignPublicIp = true,
            Subnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
            ContainerOverrides = new[]
            {
                new SfnContainerOverride
                {
                    ContainerDefinition = fargateR2MoverTaskDef.DefaultContainer!,
                    Environment = new[]
                    {
                        new SfnTaskEnvironmentVariable { Name = "TENANT_ID", Value = JsonPath.StringAt("$.TenantId") },
                        new SfnTaskEnvironmentVariable { Name = "VIDEO_ID", Value = JsonPath.StringAt("$.VideoId") },
                        new SfnTaskEnvironmentVariable { Name = "TRANSIENT_BUCKET", Value = props.TransientBucket.BucketName },
                        new SfnTaskEnvironmentVariable { Name = "PROGRESS_QUEUE_URL", Value = props.VideoProgressQueue.QueueUrl }
                    }
                }
            },
            ResultPath = "$.r2Result"
        });
        r2MoverTask.AddRetry(transientRetry);
        r2MoverTask.AddCatch(failNotificationTask, catchProps);

        var notifyPublishedTask = new SqsSendMessage(this, "NotifyPublishedTask", new SqsSendMessageProps
        {
            Queue = props.VideoPublishedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["VideoId"] = JsonPath.StringAt("$.VideoId"),
                ["TenantId"] = JsonPath.StringAt("$.TenantId"),
                ["Status"] = "Published",
                ["PlaybackUrl"] = JsonPath.Format("{}/{}/master.m3u8", JsonPath.StringAt("$.TenantId"), JsonPath.StringAt("$.VideoId")),
                ["ThumbnailUrl"] = JsonPath.Format("{}/{}/thumbnail.jpeg", JsonPath.StringAt("$.TenantId"), JsonPath.StringAt("$.VideoId")),
                ["Duration"] = JsonPath.StringAt("$.SourceMetadata.DurationFormatted"),
                ["Width"] = JsonPath.NumberAt("$.SourceMetadata.SourceWidth"),
                ["Height"] = JsonPath.NumberAt("$.SourceMetadata.SourceHeight"),
                ["EngineUsed"] = JsonPath.StringAt("$.TranscodingEngine"),
                ["TargetResourceArn"] = JsonPath.StringAt("$.TargetResourceArn")
            })
        }).Next(new Succeed(this, "PipelineSucceededState"));

        var pipelineDefinition = 
            s3EVentParserTask
            .Next(analyzeTask)
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

        PipelineStateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "events:PutTargets", "events:PutRule", "events:DescribeRule" },
            Resources = new[] { $"arn:aws:events:{Stack.Of(this).Region}:{Stack.Of(this).Account}:rule/StepFunctionsGetEventsForMediaConvertJobRule" }
        }));
        
        PipelineStateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "mediaconvert:CreateJob" },
            Resources = new[] { "*" } // Best to use arn:aws:mediaconvert... but * is fine for this action.
        }));

        PipelineStateMachine.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = new[] { "iam:PassRole" },
            Resources = new[] { props.MediaConvertRole.RoleArn }
        }));

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
                return new Vpc(scope, "TranscoderVpc", new VpcProps { MaxAzs = 1, NatGateways = 1 });
            }
        }

        return new Vpc(scope, "TranscoderVpc", new VpcProps { MaxAzs = 1, NatGateways = 1 });
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
}
