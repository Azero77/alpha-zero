using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.S3.Model;

namespace DocumentUploadedS3EventParser;

public record ParserOutput
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

[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(ParserOutput))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}

public class Function
{
    private readonly IAmazonS3 _s3Client;

    public Function()
    {
        _s3Client = new AmazonS3Client();
    }

    private static async Task Main()
    {
        Func<JsonDocument, ILambdaContext, Task<ParserOutput>> handler = new Function().FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler,
                new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
            .Build()
            .RunAsync();
    }

    public async Task<ParserOutput> FunctionHandler(JsonDocument input, ILambdaContext context)
    {
        try
        {
            var root = input.RootElement;
            
            // Navigate EventBridge payload: $.detail.bucket.name and $.detail.object.key
            var detail = root.GetProperty("detail");
            var bucket = detail.GetProperty("bucket").GetProperty("name").GetString()!;
            var key = detail.GetProperty("object").GetProperty("key").GetString()!;

            context.Logger.LogInformation($"Parsing event for bucket: {bucket}, key: {key}");

            // Key format from UploadDocumentCommand: documents/{tenantId}/{documentId}/{request.FileName}
            var parts = key.Split('/');
            if (parts.Length < 4 || parts[0] != "documents")
            {
                throw new ArgumentException($"S3 key '{key}' does not match expected format.");
            }

            var tenantId = parts[1];
            var docId = parts[2];
            var fileName = parts[3];

            // Load Metadata from S3
            var metadataRequest = new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = key
            };
            
            var metadataResponse = await _s3Client.GetObjectMetadataAsync(metadataRequest);
            
            // Note: S3 metadata keys are automatically converted to lowercase
            var documentType = metadataResponse.Metadata["x-amz-meta-document-type"] ?? "Unknown";
            var profileType = metadataResponse.Metadata["x-amz-meta-profile"] ?? "";

            return new ParserOutput
            {
                TenantId = tenantId,
                DocumentId = docId,
                S3Bucket = bucket,
                S3Key = key,
                DocumentType = documentType,
                ProfileType = profileType
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error parsing event: {ex.Message}");
            throw;
        }
    }
}