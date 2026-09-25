using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using System.Text.Json.Serialization;

namespace S3VideoCreatedEventParser;

public record S3VideoCreatedEventParserInput(
    string BucketName,
    string SourceKey);

public record S3VideoCreatedEventParserOutput(
    string VideoId,
    string TenantId,
    string SourceBucket,
    string? Description,
    string FileName,
    string Title,
    string SourceKey,
    string? TargetResourceArn = null,
    string? TranscodingEngine = "FFMPEG",
    string? EncryptionMethod = "ClearKey");

public class Function
{
    private readonly IAmazonS3 _s3Client;

    public Function()
        : this(new AmazonS3Client())
    {
    }

    public Function(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    private static async Task Main()
    {
        Func<
            S3VideoCreatedEventParserInput,
            ILambdaContext,
            Task<S3VideoCreatedEventParserOutput>
        > handler = new Function().FunctionHandler;

        await LambdaBootstrapBuilder
            .Create(
                handler,
                new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
            .Build()
            .RunAsync();
    }

    public async Task<S3VideoCreatedEventParserOutput> FunctionHandler(
        S3VideoCreatedEventParserInput input,
        ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation(
                $"Processing S3 object: " +
                $"s3://{input.BucketName}/{input.SourceKey}");

            var response = await GetObjectMetadataAsync(input, context);

            var metadata = response.Metadata;

            var videoId = GetRequiredMetadata(
                metadata,
                "videoid");

            var tenantId = GetRequiredMetadata(
                metadata,
                "tenantid");

            var targetResourceArn = GetRequiredMetadata(
                metadata,
                "targetresourcearn");

            var encryptionMethod = GetRequiredMetadata(
                metadata,
                "videoencryptionmethod");

            var transcodingMethod = GetRequiredMetadata(
                metadata,
                "videotranscodingmethod");

            var title = GetRequiredMetadata(
                metadata,
                "title");

            var fileName = GetRequiredMetadata(
                metadata,
                "file-name");

            var description = GetOptionalMetadata(
                metadata,
                "description");

            context.Logger.LogInformation(
                $"Successfully parsed metadata for video '{videoId}'.");

            return new S3VideoCreatedEventParserOutput(
                VideoId: videoId,
                TenantId: tenantId,
                SourceBucket: input.BucketName,
                Description: description,
                FileName: fileName,
                Title: title,
                SourceKey: input.SourceKey,
                TargetResourceArn: targetResourceArn,
                TranscodingEngine: transcodingMethod,
                EncryptionMethod: encryptionMethod);
        }
        catch (AmazonS3Exception ex)
        {
            context.Logger.LogError(
                $"S3 error while processing " +
                $"s3://{input.BucketName}/{input.SourceKey}. " +
                $"ErrorCode: {ex.ErrorCode}, " +
                $"StatusCode: {ex.StatusCode}, " +
                $"RequestId: {ex.RequestId}, " +
                $"Message: {ex.Message}");

            throw;
        }
        catch (AmazonServiceException ex)
        {
            context.Logger.LogError(
                $"AWS service error while processing " +
                $"s3://{input.BucketName}/{input.SourceKey}. " +
                $"StatusCode: {ex.StatusCode}, " +
                $"Message: {ex.Message}");

            throw;
        }
        catch (InvalidOperationException ex)
        {
            context.Logger.LogError(
                $"Invalid video metadata for " +
                $"s3://{input.BucketName}/{input.SourceKey}. " +
                $"Message: {ex.Message}");

            throw;
        }
        catch (UriFormatException ex)
        {
            context.Logger.LogError(
                $"Invalid URL-encoded metadata for " +
                $"s3://{input.BucketName}/{input.SourceKey}. " +
                $"Message: {ex.Message}");

            throw;
        }
        catch (ArgumentException ex)
        {
            context.Logger.LogError(
                $"Invalid argument while processing " +
                $"s3://{input.BucketName}/{input.SourceKey}. " +
                $"Message: {ex.Message}");

            throw;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                $"Unexpected error while processing " +
                $"s3://{input.BucketName}/{input.SourceKey}. " +
                $"Exception: {ex}");

            throw;
        }
    }

    private async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(
        S3VideoCreatedEventParserInput input,
        ILambdaContext context)
    {
        try
        {
            return await _s3Client.GetObjectMetadataAsync(
                input.BucketName,
                input.SourceKey);
        }
        catch (AmazonS3Exception ex)
        {
            context.Logger.LogError(
                $"Failed to read S3 metadata. " +
                $"Bucket: {input.BucketName}, " +
                $"Key: {input.SourceKey}, " +
                $"ErrorCode: {ex.ErrorCode}, " +
                $"StatusCode: {ex.StatusCode}, " +
                $"Message: {ex.Message}");

            throw;
        }
    }

    private static string GetRequiredMetadata(
        MetadataCollection metadata,
        string key)
    {
        var value = metadata[$"x-amz-meta-{key}"];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required S3 metadata '{key}' is missing.");
        }

        return Uri.UnescapeDataString(value);
    }

    private static string? GetOptionalMetadata(
        MetadataCollection metadata,
        string key)
    {
        var value = metadata[$"x-amz-meta-{key}"];

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.UnescapeDataString(value);
    }
}

[JsonSerializable(typeof(S3VideoCreatedEventParserInput))]
[JsonSerializable(typeof(S3VideoCreatedEventParserOutput))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
}