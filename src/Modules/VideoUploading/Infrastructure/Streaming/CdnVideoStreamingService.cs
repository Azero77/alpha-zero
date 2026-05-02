using AlphaZero.Modules.VideoUploading.Application.Streaming.Queries;
using AlphaZero.Modules.VideoUploading.Application.Repositories;
using Aspire.Shared;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Streaming;

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

public class DatabaseCloudFlareCdnVideoStreamingService(AWSResources resources, IVideoRepository videoRepository) : IStreamingService
{
    public async Task<ErrorOr<StreamingInfoResponseDTO>> GetStreamingInfo(Guid videoId)
    {
        var video = await videoRepository.GetByIdAsync(videoId);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {videoId} was not found.");
        }

        var domain = resources.CdnDomain;
        
        // We can now return the real streaming URL stored in the DB if available, 
        // or construct it if we follow a standard pattern.
        var streamingUrl = video.OutputFolder != null 
            ? $"http://{domain}/{video.OutputFolder}"
            : $"http://{domain}/streaming/{videoId}/master.m3u8";

        var response = new StreamingInfoResponseDTO(
            url: streamingUrl,
            encryptionMethod: "ClearKey",
            licenseUrl: $"/api/video/keys/{videoId}");

        return response.ToErrorOr();
    }
}
