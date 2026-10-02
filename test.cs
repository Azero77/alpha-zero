using System;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;

class Program
{
    static void Main()
    {
        var builder = new ConfigurationBuilder()
            .AddEnvironmentVariables();
        var config = builder.Build();
        var options = config.GetAWSOptions();
        Console.WriteLine($"Region: {options.Region?.SystemName ?? "NULL"}");
    }
}
