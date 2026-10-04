using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace AlphaZero.Cdk.Constructs;

public class ImagePipelineConstructProps
{
    public required IBucket InputBucket { get; set; }
    public required IBucket CdnBucket { get; set; }
    public required IQueue DocumentProcessingCompletedQueue { get; set; }
    public required IQueue DocumentProcessingFaultedQueue { get; set; }
}

public class ImagePipelineConstruct : Construct
{
    public StateMachine PipelineStateMachine { get; }
    public Function ProcessImageLambda { get; }

    public ImagePipelineConstruct(Construct scope, string id, ImagePipelineConstructProps props) : base(scope, id)
    {
        // 1. Define Lambda
        ProcessImageLambda = new Function(this, "ProcessImageLambda", new FunctionProps
        {
            FunctionName = "alphazero-process-image",
            Runtime = Runtime.PROVIDED_AL2023,
            Handler = "bootstrap",
            Code = Code.FromAsset("src/lambdas/AlphaZero.ImageProcessing.Lambda/publish"),
            Timeout = Duration.Minutes(2),
            MemorySize = 1536
        });

        // Grant S3 permissions
        props.InputBucket.GrantRead(ProcessImageLambda);
        props.CdnBucket.GrantReadWrite(ProcessImageLambda);

        // 2. Step Functions Tasks
        var processImageTask = new LambdaInvoke(this, "ProcessImageTask", new LambdaInvokeProps
        {
            LambdaFunction = ProcessImageLambda,
            PayloadResponseOnly = true,
            Payload = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["InputBucket"] = props.InputBucket.BucketName,
                ["InputKey"] = JsonPath.StringAt("$.detail.message.s3Key"),
                ["OutputBucket"] = props.CdnBucket.BucketName,
                ["OutputPrefix"] = JsonPath.Format("{}/{}", JsonPath.StringAt("$.detail.message.tenantId"), JsonPath.StringAt("$.detail.message.documentId"))
            }),
            ResultPath = "$.ProcessResult"
        });

        var notifySuccessTask = new SqsSendMessage(this, "NotifySuccessTask", new SqsSendMessageProps
        {
            Queue = props.DocumentProcessingCompletedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["DocumentId"] = JsonPath.StringAt("$.detail.message.documentId"),
                ["TenantId"] = JsonPath.StringAt("$.detail.message.tenantId"),
                ["Status"] = "Completed",
                ["PayloadJson"] = JsonPath.JsonToString(JsonPath.ObjectAt("$.ProcessResult"))
            })
        });

        var notifyFailureTask = new SqsSendMessage(this, "NotifyFailureTask", new SqsSendMessageProps
        {
            Queue = props.DocumentProcessingFaultedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["DocumentId"] = JsonPath.StringAt("$.detail.message.documentId"),
                ["TenantId"] = JsonPath.StringAt("$.detail.message.tenantId"),
                ["ErrorMessage"] = JsonPath.StringAt("$.errorInfo.Cause")
            })
        }).Next(new Fail(this, "PipelineFailedState"));

        var catchProps = new CatchProps
        {
            ResultPath = "$.errorInfo"
        };
        processImageTask.AddCatch(notifyFailureTask, catchProps);

        // 3. Define State Machine
        var definition = processImageTask.Next(notifySuccessTask);

        PipelineStateMachine = new StateMachine(this, "AlphaZeroImagePipelineStateMachine", new StateMachineProps
        {
            StateMachineName = "AlphaZero-ImagePipeline",
            DefinitionBody = DefinitionBody.FromChainable(definition),
            Timeout = Duration.Minutes(5)
        });

        // 4. EventBridge Trigger from MassTransit
        var eventRule = new Amazon.CDK.AWS.Events.Rule(this, "ImageProcessingRule", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                // MassTransit typically puts the message type in detail-type or we can match on the detail.message structure
                DetailType = new[] { "DocumentProcessingRequestedEvent" }
            }
        });
        eventRule.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));
    }
}
