using Amazon.CDK;
using Amazon.CDK.AWS.StepFunctions;
using AlphaZero.Cdk.Constructs;
using Constructs;

namespace AlphaZero.Cdk.Stacks;

public class ImagePipelineStackProps : StackProps
{
    public required StorageStack Storage { get; set; }
}

public class ImagePipelineStack : Stack
{
    public StateMachine PipelineStateMachine { get; }
    public DocumentPipelineConstruct Pipeline { get; }

    public ImagePipelineStack(Construct scope, string id, ImagePipelineStackProps props) : base(scope, id, props)
    {
        var storage = props.Storage;

        Pipeline = new DocumentPipelineConstruct(this, "DocumentPipeline", new DocumentPipelineConstructProps
        {
            DocumentInputBucketPrivate = storage.DocumentInputBucketPrivate,
            DocumentInputBucketPublic = storage.DocumentInputBucketPublic,
            DocumentProcessingCompletedQueue = storage.DocumentProcessingCompletedQueue,
            DocumentProcessingFaultedQueue = storage.DocumentProcessingFaultedQueue
        });

        PipelineStateMachine = Pipeline.PipelineStateMachine;

        new CfnOutput(this, "DocumentPipelineStepFunctionArnOutput", new CfnOutputProps { Value = PipelineStateMachine.StateMachineArn });
    }
}
