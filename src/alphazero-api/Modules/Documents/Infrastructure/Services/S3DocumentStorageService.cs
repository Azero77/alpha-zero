using AlphaZero.Modules.Documents.Application.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Shared;

namespace AlphaZero.Modules.Documents.Infrastructure.Services;

public class S3DocumentStorageService : IDocumentStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3DocumentStorageService(IAmazonS3 s3Client, AWSResources awsResources)
    {
        _s3Client = s3Client;
        _bucketName = awsResources.InputS3?.BucketName ?? "alphazero-documents";
    }

    public async Task<string> GenerateUploadPresignedUrlAsync(string key, string contentType, TimeSpan expiresIn)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiresIn)
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    public async Task<string> GenerateDownloadPresignedUrlAsync(string key, string fileName, TimeSpan expiresIn)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn),
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"attachment; filename=\"{Uri.EscapeDataString(fileName)}\""
            }
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }
}
