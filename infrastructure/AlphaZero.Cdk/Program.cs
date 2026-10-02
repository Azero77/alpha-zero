using Amazon.CDK;
using AlphaZero.Cdk.Stacks;

namespace AlphaZero.Cdk;

public static class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        var env = new Amazon.CDK.Environment
        {
            Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
            Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
        };

        var storageStack = new StorageStack(app, "AlphaZeroStorageStack", new StorageStackProps
        {
            Environment = "prod",
            Env = env
        });

        new VideoPipelineStack(app, "AlphaZeroVideoPipelineStack", new VideoPipelineStackProps
        {
            Storage = storageStack,
            Env = env
        });

        app.Synth();
    }
}
