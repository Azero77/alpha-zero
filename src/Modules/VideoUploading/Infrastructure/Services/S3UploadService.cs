using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Infrastructure.Persistance;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Shared;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class S3UploadService : IUploadService
{

    private readonly IAmazonS3 _client;
    private readonly S3Settings _s3Settings;
    private const string UnSegmentedFilesFolder = "files";
    private const string SegmentedFilesFolder = "dash";


    public S3UploadService(IAmazonS3 client, S3Settings s3Settings)
    {
        _client = client;
        _s3Settings = s3Settings;
    }

    public async Task<ErrorOr<GetPresignedUrlResponse>> GetFile(string key)
    {
        try
        {
            var request = new GetPreSignedUrlRequest()
            {
                BucketName = _s3Settings.BucketName,
                Key = $"{UnSegmentedFilesFolder}/{key}",
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(15),
            };
            var result = await _client.GetPreSignedURLAsync(request);
            return new GetPresignedUrlResponse(key, result);
        }
        catch (AmazonS3Exception exception)
        {
            return Error.Failure("Resources.Failure", exception.Message);
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

    public async Task<ErrorOr<GetPresignedUrlResponse>> UploadFile(string fileName, string contentType)
    {
        try
        {
            Guid key = Guid.NewGuid();
            var request = new GetPreSignedUrlRequest()
            {
                BucketName = _s3Settings.BucketName,
                ContentType = contentType,
                Metadata = {
                        ["file-name"] = fileName
                    },
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                Key = $"{UnSegmentedFilesFolder}/{key}"
            };

            string url = await _client.GetPreSignedURLAsync(request);

            return new GetPresignedUrlResponse(key.ToString(), url);
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
