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
public record S3DocumentCreatedEventParserInput(
    string BucketName,
    string SourceKey);
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

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; init; } = false;
}

[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(ParserOutput))]
[JsonSerializable(typeof(S3DocumentCreatedEventParserInput))]
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
        Func<S3DocumentCreatedEventParserInput, ILambdaContext, Task<ParserOutput>> handler = new Function().FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler,
                new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
            .Build()
            .RunAsync();
    }

    public async Task<ParserOutput> FunctionHandler(S3DocumentCreatedEventParserInput input, ILambdaContext context)
    {
        try
        {
            var bucket = input.BucketName;
            var key = input.SourceKey;
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
                ProfileType = profileType,
                IsPublic = bucket.Contains("-public-")
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error parsing event: {ex.Message}");
            throw;
        }
    }
}