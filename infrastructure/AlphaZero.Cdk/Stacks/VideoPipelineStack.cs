using Amazon.CDK;
using Amazon.CDK.AWS.StepFunctions;
using AlphaZero.Cdk.Constructs;
using Constructs;

namespace AlphaZero.Cdk.Stacks;

public class VideoPipelineStackProps : StackProps
{
    public required StorageStack Storage { get; set; }
}

public class VideoPipelineStack : Stack
{
    public StateMachine PipelineStateMachine { get; }
    public VideoPipelineConstruct Pipeline { get; }

    public VideoPipelineStack(Construct scope, string id, VideoPipelineStackProps props) : base(scope, id, props)
    {
        var storage = props.Storage;

        Pipeline = new VideoPipelineConstruct(this, "VideoPipeline", new VideoPipelineConstructProps
        {
            InputBucket = storage.InputBucket,
            TransientBucket = storage.TransientBucket,
            VideoPublishedQueue = storage.VideoPublishedQueue,
            VideoFailedQueue = storage.VideoFailedQueue,
            VideoProgressQueue = storage.VideoProgressQueue,
            MasterClearKey = storage.MasterClearKey,
            R2Credentials = storage.R2Credentials
        });

        PipelineStateMachine = Pipeline.PipelineStateMachine;
    }
}
