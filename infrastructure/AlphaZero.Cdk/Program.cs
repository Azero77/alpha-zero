using Amazon.CDK;
using AlphaZero.Cdk.Stacks;

namespace AlphaZero.Cdk;

public static class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        var storageStack = new StorageStack(app, "AlphaZeroStorageStack", new StorageStackProps
        {
            Environment = "prod"
        });

        new VideoPipelineStack(app, "AlphaZeroVideoPipelineStack", new VideoPipelineStackProps
        {
            Storage = storageStack
        });

        app.Synth();
    }
}
