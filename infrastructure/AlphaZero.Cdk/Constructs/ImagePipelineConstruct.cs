using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace AlphaZero.Cdk.Constructs;

public class DocumentPipelineConstructProps
{
    public required IBucket DocumentInputBucketPrivate { get; set; }
    public required IBucket DocumentInputBucketPublic { get; set; }
    public required IQueue DocumentProcessingCompletedQueue { get; set; }
    public required IQueue DocumentProcessingFaultedQueue { get; set; }
}

public class DocumentPipelineConstruct : Construct
{
    public StateMachine PipelineStateMachine { get; }
    public Function ParserLambda { get; }
    public Function MoverLambda { get; }

    public DocumentPipelineConstruct(Construct scope, string id, DocumentPipelineConstructProps props) : base(scope, id)
    {
        // 1. Define Lambdas
        ParserLambda = new Function(this, "InputS3ImageUploadedEventParser", new FunctionProps
        {
            FunctionName = "alphazero-image-parser",
            Runtime = Runtime.PROVIDED_AL2023,
            Handler = "bootstrap",
            Code = Code.FromAsset("src/lambdas/InputS3ImageUploadedEventParser/publish"),
            Timeout = Duration.Seconds(30),
            MemorySize = 256
        });

        MoverLambda = new Function(this, "ImageProcessorAndMoverToR2", new FunctionProps
        {
            FunctionName = "alphazero-process-and-move-to-r2",
            Runtime = Runtime.PROVIDED_AL2023,
            Handler = "bootstrap",
            Code = Code.FromAsset("src/lambdas/ImageProcessorAndMoverToR2/publish"),
            Timeout = Duration.Minutes(2),
            MemorySize = 1536
        });

        // Grant S3 permissions
        props.DocumentInputBucketPrivate.GrantRead(MoverLambda);
        props.DocumentInputBucketPublic.GrantRead(MoverLambda);
        props.DocumentInputBucketPrivate.GrantRead(ParserLambda);
        props.DocumentInputBucketPublic.GrantRead(ParserLambda);

        // Grant SSM permission for R2 credentials
        MoverLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = new[] { "ssm:GetParameter" },
            Resources = new[] { $"arn:aws:ssm:{Stack.Of(this).Region}:{Stack.Of(this).Account}:parameter/AlphaZero/R2Credentials" }
        }));

        // 2. Step Functions Tasks & Failure Handling
        var catchProps = new CatchProps
        {
            ResultPath = "$.errorInfo"
        };

        var notifyFailureTask = new SqsSendMessage(this, "NotifyFailureTask", new SqsSendMessageProps
        {
            Queue = props.DocumentProcessingFaultedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["DocumentId"] = JsonPath.StringAt("$.ParsedEvent.documentId"),
                ["TenantId"] = JsonPath.StringAt("$.ParsedEvent.tenantId"),
                ["Status"] = "Failed",
                ["Error"] = new Dictionary<string, object>
                {
                    ["errorType"] = JsonPath.StringAt("$.errorInfo.Error"),
                    ["cause"] = JsonPath.StringAt("$.errorInfo.Cause")
                }
            })
        }).Next(new Fail(this, "PipelineFailedState"));

        var parseEventTask = new LambdaInvoke(this, "ParseEventTask", new LambdaInvokeProps
        {
            LambdaFunction = ParserLambda,
            PayloadResponseOnly = true,
            ResultPath = "$.ParsedEvent",
            Payload = TaskInput.FromObject(new Dictionary<string, object>()
            {
                ["BucketName"] = JsonPath.StringAt("$.detail.bucket.name"),
                ["SourceKey"] = JsonPath.StringAt( "$.detail.object.key")
            }) 
        });
        parseEventTask.AddCatch(notifyFailureTask, catchProps);

        var processImageTask = new LambdaInvoke(this, "ProcessImageTask", new LambdaInvokeProps
        {
            LambdaFunction = MoverLambda,
            PayloadResponseOnly = true,
            Payload = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["tenantId"] = JsonPath.StringAt("$.ParsedEvent.tenantId"),
                ["documentId"] = JsonPath.StringAt("$.ParsedEvent.documentId"),
                ["s3Bucket"] = JsonPath.StringAt("$.ParsedEvent.s3Bucket"),
                ["s3Key"] = JsonPath.StringAt("$.ParsedEvent.s3Key"),
                ["documentType"] = JsonPath.StringAt("$.ParsedEvent.documentType"),
                ["profileType"] = JsonPath.StringAt("$.ParsedEvent.profileType")
            }),
            ResultPath = "$.ProcessResult"
        });
        processImageTask.AddCatch(notifyFailureTask, catchProps);

        var notifySuccessTask = new SqsSendMessage(this, "NotifySuccessTask", new SqsSendMessageProps
        {
            Queue = props.DocumentProcessingCompletedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["DocumentId"] = JsonPath.StringAt("$.ParsedEvent.documentId"),
                ["TenantId"] = JsonPath.StringAt("$.ParsedEvent.tenantId"),
                ["Status"] = "Completed",
                ["PayloadJson"] = JsonPath.JsonToString(JsonPath.ObjectAt("$.ProcessResult"))
            })
        });


        var notifyNonImageSuccessTask = new SqsSendMessage(this, "NotifyNonImageSuccessTask", new SqsSendMessageProps
        {
            Queue = props.DocumentProcessingCompletedQueue,
            MessageBody = TaskInput.FromObject(new Dictionary<string, object>
            {
                ["DocumentId"] = JsonPath.StringAt("$.ParsedEvent.documentId"),
                ["TenantId"] = JsonPath.StringAt("$.ParsedEvent.tenantId"),
                ["Status"] = "Completed",
                ["PayloadJson"] = "{}"
            })
        });
        
        var router = new Choice(this, "DocumentTypeRouter")
            .When(Condition.StringEquals("$.ParsedEvent.documentType", "Image"), processImageTask.Next(notifySuccessTask))
            .Otherwise(notifyNonImageSuccessTask);

        // 3. Define State Machine
        var definition = parseEventTask.Next(router);

        PipelineStateMachine = new StateMachine(this, "AlphaZeroDocumentPipelineStateMachine", new StateMachineProps
        {
            StateMachineName = "AlphaZero-DocumentPipeline",
            DefinitionBody = DefinitionBody.FromChainable(definition),
            Timeout = Duration.Minutes(5)
        });

        /*
        // 4. EventBridge Trigger from S3 ObjectCreated
        var eventRulePrivate = new Amazon.CDK.AWS.Events.Rule(this, "ImageProcessingRulePrivate", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.InputBucketPrivate.BucketName } } } }
                }
            }
        });
        eventRulePrivate.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));

        var eventRulePublic = new Amazon.CDK.AWS.Events.Rule(this, "ImageProcessingRulePublic", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.InputBucketPublic.BucketName } } } }
                }
            }
        });*/
        //eventRulePublic.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));

        var documentEventRulePrivate = new Amazon.CDK.AWS.Events.Rule(this, "DocumentProcessingRulePrivate", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.DocumentInputBucketPrivate.BucketName } } } }
                }
            }
        });
        documentEventRulePrivate.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));

        var documentEventRulePublic = new Amazon.CDK.AWS.Events.Rule(this, "DocumentProcessingRulePublic", new Amazon.CDK.AWS.Events.RuleProps
        {
            EventPattern = new Amazon.CDK.AWS.Events.EventPattern
            {
                Source = new[] { "aws.s3" },
                DetailType = new[] { "Object Created" },
                Detail = new Dictionary<string, object>
                {
                    { "bucket", new Dictionary<string, object> { { "name", new[] { props.DocumentInputBucketPublic.BucketName } } } }
                }
            }
        });
        documentEventRulePublic.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(PipelineStateMachine));
    }
}
