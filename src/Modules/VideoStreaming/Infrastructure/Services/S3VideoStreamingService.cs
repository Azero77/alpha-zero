
using AlphaZero.Modules.VideoStreaming.Application.Queries;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Shared;
using ErrorOr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime;
using static Aspire.Shared.AWSResources;



public class S3VideoStreamingService(AWSResources resources, IConfiguration configuration) : IStreamingService
{
    public Task<ErrorOr<StreamingInfoResponseDTO>> GetStreamingInfo(Guid videoId)
    {
        string bucket = resources!.CdnS3!.BucketName;
        var result = new StreamingInfoResponseDTO(
            url: $"https://{bucket}.s3.amazonaws.com/streaming/{videoId}/master.m3u8",
            encryptionMethod: "ClearKey",
            licenseUrl: $"/api/video/keys/{videoId}"); // Relative to API Base

        return Task.FromResult(result.ToErrorOr());
    }
}

public class CloudFlareCdnVideoStreamingService(AWSResources resources) : IStreamingService
{
    public Task<ErrorOr<StreamingInfoResponseDTO>> GetStreamingInfo(Guid videoId)
    {
        var domain = resources.CdnDomain;

        var response = new StreamingInfoResponseDTO(
            url: $"http://{domain}/streaming/{videoId}/master.m3u8",
            encryptionMethod: "ClearKey",
            licenseUrl: $"/api/video/keys/{videoId}");

        return Task.FromResult(response.ToErrorOr());
    }
}