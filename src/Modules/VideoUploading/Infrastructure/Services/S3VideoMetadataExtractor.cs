using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Modules.VideoUploading.Domain.Services;
using Amazon.S3;
using ErrorOr;
using FFMpegCore;

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Services;

public class S3VideoSpecificationExtractor(IUploadService service) : IVideoSpecificationExtractorService
{
    public async Task<ErrorOr<VideoSpecifications>> ExtractAsync(Video video, CancellationToken token = default)
    {
        return await ExtractAsync(video.SourceKey, token);
    }

    public async Task<ErrorOr<VideoSpecifications>> ExtractAsync(string sourceKey, CancellationToken token = default)
    {
        var request = await service.GetFile(sourceKey);
        if (request.IsError)
            return request.Errors;
            
        var url = request.Value.presignedUrl;
        
        var mediaInfo = await FFProbe.AnalyseAsync(new Uri(url), cancellationToken: token);

        var videoStream = mediaInfo.PrimaryVideoStream;
        if (videoStream is null)
        {
            return Error.Failure("VideoUploading.Infrastructure.Media", "No Primary VideoStream for video at " + sourceKey);
        }

        return new VideoSpecifications(mediaInfo.Duration,
            new Resolution(videoStream.Width, videoStream.Height));
    }
}
