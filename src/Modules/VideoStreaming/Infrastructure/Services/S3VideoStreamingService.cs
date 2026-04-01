
using AlphaZero.Modules.VideoStreaming.Application.Queries;
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Shared;
using ErrorOr;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime;
using static Aspire.Shared.AWSResources;



public class S3VideoStreamingService(AWSResources resources,IConfiguration configuration) : IStreamingService
{

    
    public Task<ErrorOr<StreamingInfoResponseDTO>> GetStreamingInfo(Guid videoId)
    {
        string bucket = resources!.CdnS3!.BucketName;
        string region = configuration.GetAWSOptions()
            .Region.SystemName;
        var result = new StreamingInfoResponseDTO(
            $"https://{bucket}.s3.amazonaws.com/{videoId}/master.m3u8",
            videoId.ToString().Replace("-", ""));
        return Task.FromResult(result.ToErrorOr());
    }
}