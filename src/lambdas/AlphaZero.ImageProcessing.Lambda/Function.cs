using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using AlphaZero.ImageProcessing;
using System.Text.Json.Serialization;

namespace AlphaZero.ImageProcessing.Lambda;

public record ProcessImageRequest
{
    public string InputBucket { get; init; } = string.Empty;
    public string InputKey { get; init; } = string.Empty;
    public string OutputBucket { get; init; } = string.Empty;
    public string OutputPrefix { get; init; } = string.Empty;
}

public record ProcessImageResponse
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = string.Empty;
    public Dictionary<string, string> ExifData { get; init; } = new();
    public Dictionary<string, string> Variants { get; init; } = new();
}

[JsonSerializable(typeof(ProcessImageRequest))]
[JsonSerializable(typeof(ProcessImageResponse))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}

public class Function
{
    private readonly IAmazonS3 _s3Client;
    private readonly IImageProcessor _imageProcessor;

    public Function()
    {
        _s3Client = new AmazonS3Client();
        _imageProcessor = new ImageSharpProcessor();
    }

    public Function(IAmazonS3 s3Client, IImageProcessor imageProcessor)
    {
        _s3Client = s3Client;
        _imageProcessor = imageProcessor;
    }

    private static async Task Main()
    {
        Func<ProcessImageRequest, ILambdaContext, Task<ProcessImageResponse>> handler = new Function().FunctionHandler;

        await LambdaBootstrapBuilder
            .Create(handler, new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
            .Build()
            .RunAsync();
    }

    public async Task<ProcessImageResponse> FunctionHandler(ProcessImageRequest request, ILambdaContext context)
    {
        if (string.IsNullOrEmpty(request.InputBucket) || string.IsNullOrEmpty(request.InputKey))
        {
            throw new ArgumentException("InputBucket and InputKey must be provided.");
        }

        if (string.IsNullOrEmpty(request.OutputBucket) || string.IsNullOrEmpty(request.OutputPrefix))
        {
            throw new ArgumentException("OutputBucket and OutputPrefix must be provided.");
        }

        context.Logger.LogInformation($"Processing image {request.InputKey} from {request.InputBucket}");

        string tmpDir = Path.Combine("/tmp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            // 1. Download to /tmp
            string inputFilePath = Path.Combine(tmpDir, "input_image");
            
            var getObjRequest = new GetObjectRequest
            {
                BucketName = request.InputBucket,
                Key = request.InputKey
            };
            
            using (var response = await _s3Client.GetObjectAsync(getObjRequest))
            {
                await response.WriteResponseStreamToFileAsync(inputFilePath, false, default);
            }
            
            // 2. Process
            string outputDir = Path.Combine(tmpDir, "output");
            var result = await _imageProcessor.ProcessAsync(inputFilePath, outputDir);

            // 3. Upload to OutputBucket
            var variantsS3Keys = new Dictionary<string, string>();
            
            foreach (var kvp in result.Variants)
            {
                string variantName = kvp.Key;
                string localFilePath = kvp.Value;
                string s3Key = $"{request.OutputPrefix.TrimEnd('/')}/{variantName}.webp";
                
                var putRequest = new PutObjectRequest
                {
                    BucketName = request.OutputBucket,
                    Key = s3Key,
                    FilePath = localFilePath,
                    ContentType = "image/webp"
                };
                
                await _s3Client.PutObjectAsync(putRequest);
                variantsS3Keys[variantName] = s3Key;
            }

            // 4. Return
            return new ProcessImageResponse
            {
                Width = result.Width,
                Height = result.Height,
                Format = result.Format,
                ExifData = result.ExifData,
                Variants = variantsS3Keys
            };
        }
        finally
        {
            // Cleanup /tmp
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }
}
