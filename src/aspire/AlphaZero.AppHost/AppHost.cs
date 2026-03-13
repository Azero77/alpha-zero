var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AlphaZero_API>("alphazero-api");

builder.Build().Run();
