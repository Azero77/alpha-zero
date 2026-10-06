using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AlphaZero.ImageProcessing;
using AlphaZero.ImageProcessing.Strategies;
using AlphaZero.Modules.Documents.Domain.Models;

namespace ImageProcessorAndMoverToR2;

public record ProcessImageRequest
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; init; } = string.Empty;
    
    [JsonPropertyName("documentId")]
    public string DocumentId { get; init; } = string.Empty;
    
    [JsonPropertyName("s3Bucket")]
    public string S3Bucket { get; init; } = string.Empty;
    
    [JsonPropertyName("s3Key")]
    public string S3Key { get; init; } = string.Empty;
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; } = string.Empty;
    
    [JsonPropertyName("profileType")]
    public string ProfileType { get; init; } = string.Empty;
}

public record ProcessImageResponse
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = string.Empty;
    public Dictionary<string, string> ExifData { get; init; } = new();
    public Dictionary<string, string> Variants { get; init; } = new();
}

public class R2Credentials
{
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
}

[JsonSerializable(typeof(ProcessImageRequest))]
[JsonSerializable(typeof(ProcessImageResponse))]
[JsonSerializable(typeof(R2Credentials))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}

public class Function
{
    private readonly IAmazonS3 _awsS3Client = new AmazonS3Client();
    private readonly IAmazonSimpleSystemsManagement _ssmClient = new AmazonSimpleSystemsManagementClient();

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
        if (string.IsNullOrEmpty(request.S3Bucket) || string.IsNullOrEmpty(request.S3Key))
            throw new ArgumentException("S3Bucket and S3Key must be provided.");

        context.Logger.LogInformation($"Processing image {request.S3Key} from {request.S3Bucket}");

        // 1. Fetch R2 credentials
        var credRequest = new GetParameterRequest
        {
            Name = "/AlphaZero/VideoPipeline/R2Credentials",
            WithDecryption = true
        };

        var credResponse = await _ssmClient.GetParameterAsync(credRequest);
        var r2Creds = JsonSerializer.Deserialize(credResponse.Parameter.Value, LambdaFunctionJsonSerializerContext.Default.R2Credentials);
        
        if (r2Creds == null)
            throw new Exception("Failed to parse R2 credentials");

        var r2Config = new AmazonS3Config
        {
            ServiceURL = r2Creds.ServiceUrl,
        };
        var r2Client = new AmazonS3Client(r2Creds.AccessKeyId, r2Creds.SecretAccessKey, r2Config);

        string tmpDir = Path.Combine("/tmp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            // 2. Download to /tmp
            string inputFilePath = Path.Combine(tmpDir, "input_image");
            
            var getObjRequest = new GetObjectRequest
            {
                BucketName = request.S3Bucket,
                Key = request.S3Key
            };
            
            using (var response = await _awsS3Client.GetObjectAsync(getObjRequest))
            {
                await response.WriteResponseStreamToFileAsync(inputFilePath, false, default);
            }
            
            // 3. Process
            string outputDir = Path.Combine(tmpDir, "output");
            
            ImageProcessorFactory factory = new([
                new CourseCoverProcessingStrategy(),
                new VideoThumbnailProcessingStrategy(),
                new InlineImageProcessingStrategy(),
                new AvatarProcessingStrategy()
            ]);
            var processor = factory.GetStrategy(request.ProfileType);
            
            var result = await processor.ProcessAsync(inputFilePath, outputDir);

            // 4. Upload to R2 OutputBucket
            var variantsS3Keys = new Dictionary<string, string>();
            
            foreach (var kvp in result.Variants)
            {
                string variantName = kvp.Key;
                string localFilePath = kvp.Value;
                // format: documents/{tenantId}/{documentId}/{variant}.webp
                
                string s3Key = DocumentFileStorageConstants.GetDocumentS3Key(request.TenantId, request.DocumentId,$"{variantName}.webp" );
                
                var putRequest = new PutObjectRequest
                {
                    BucketName = r2Creds.BucketName,
                    Key = s3Key,
                    FilePath = localFilePath,
                    ContentType = "image/webp",
                    DisablePayloadSigning = true
                };
                
                await r2Client.PutObjectAsync(putRequest);
                variantsS3Keys[variantName] = s3Key;
            }

            // 5. Return
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
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }
        }
    }
}