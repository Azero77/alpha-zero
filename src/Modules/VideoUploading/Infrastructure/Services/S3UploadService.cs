using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using AlphaZero.Modules.VideoUploading.IntegrationEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Shared;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class S3UploadService : IUploadService
{

    private readonly IAmazonS3 _client;
    private readonly S3Settings _s3Settings;
    private const string FilesFolder = "files";
    public const string VideoIdMetaDataHeader = "VideoId";


    public S3UploadService(IAmazonS3 client, AWSResources aWSResources)
    {
        _client = client;
        _s3Settings = aWSResources.InputS3 ?? throw new ArgumentException("S3 input is not configured");
    }

    public async Task<ErrorOr<GetPresignedUrlResponse>> GetFile(string key)
    {
        try
        {
            var request = new GetPreSignedUrlRequest()
            {
                BucketName = _s3Settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddHours(1),
            };
            var result = await _client.GetPreSignedURLAsync(request);
            return new GetPresignedUrlResponse(key, result);
        }
        catch (AmazonS3Exception exception)
        {
            return Error.Failure("Resources.Failure", exception.Message);
        }
    }
    public async Task<ErrorOr<Dictionary<string, object>>> GetMetadata(string key)
    {
        try
        {
            var metadataRequest = new GetObjectMetadataRequest()
            {
                BucketName = _s3Settings.BucketName,
                Key = key,
            };

            var metadataResponse = await _client.GetObjectMetadataAsync(metadataRequest);

            var metadata = metadataResponse.Metadata!.Keys
                       .ToDictionary(m => m.StartsWith("x-amz-meta-",
                       StringComparison.InvariantCultureIgnoreCase) ?  m.Substring("x-amz-meta-".Length): m, m => (object)metadataResponse.Metadata[m]);

            metadata["Content-Length"] = metadataResponse.ContentLength;
            metadata["Content-Type"] = metadataResponse.Headers.ContentType;

            return metadata;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Error.NotFound();
        }
        catch (Exception ex)
        {
            return Error.Failure("S3.MetadataError", ex.Message);
        }
    }

    public async Task<List<GetPresignedUrlResponse?>> GetFiles(List<string> keysAsList)
    {
        List<Task<GetPresignedUrlResponse>> tasks = new();
        foreach (var key in keysAsList)
        {
            var key_result = GetFile(key);
        }
        await Task.WhenAll(tasks);
        return tasks.Select(r => r.IsCompletedSuccessfully ? r.Result : null).ToList();
    }

    public async Task<ErrorOr<GetPresignedUrlResponse>> UploadFile(string fileName, string contentType, Dictionary<string,string>? metadata = null)
    {
        try
        {
            Guid guid = Guid.NewGuid();
            string key = $"{FilesFolder}/{guid}";
            var request = new GetPreSignedUrlRequest()
            {
                BucketName = _s3Settings.BucketName,
                ContentType = contentType,
                Metadata = {
                        ["file-name"] = Uri.EscapeDataString(fileName)
                    },
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                Key = key,

            };
            if (metadata is not null)
            {
                foreach (var pair in metadata)
                {
                    request.Metadata.Add(pair.Key.ToLowerInvariant(), Uri.EscapeDataString(pair.Value));
                }
            }

            string url = await _client.GetPreSignedURLAsync(request);

            return new GetPresignedUrlResponse(key, url);
        }
        catch (AmazonS3Exception exception)
        {
            return Error.Failure("Resources.Failure", exception.Message);
        }
    }
    public async Task<ErrorOr<Success>> DeleteFile(string key)
    {
        DeleteObjectRequest request = new DeleteObjectRequest()
        {
            BucketName = _s3Settings.BucketName,
            Key = key,
        };
        var response = await _client.DeleteObjectAsync(request);
        int code = (int)response.HttpStatusCode;
        return code >= 200 && code < 300 ? Result.Success : Error.Failure();
    }
}
