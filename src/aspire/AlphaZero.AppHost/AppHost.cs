var builder = DistributedApplication.CreateBuilder(args);


var awsSdkConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.EUNorth1);


var awscdkStack = builder.AddAWSCDKStack("AlphaZero-Aspire")
    .WithReference(awsSdkConfig);

var s3 = awscdkStack.AddS3Bucket("s3");

var api = builder.AddProject<Projects.AlphaZero_API>("alphazero-api")
    .WithReference(awsSdkConfig)
    .WithReference(s3);


builder.Build().Run();
