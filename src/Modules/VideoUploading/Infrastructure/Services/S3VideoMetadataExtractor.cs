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
        return new VideoSpecifications(TimeSpan.FromHours(1),new Resolution(1920,1080));
        var request = await service.GetFile(video.SourceKey);
        if (request.IsError)
            return request.Errors;
        var url = request.Value.presignedUrl;


        var mediaInfo = await FFProbe.AnalyseAsync(url, customArguments : 
            "-protocol_whitelist file,http,https,tcp,tls"
            , cancellationToken: token);

        var videoStream = mediaInfo.PrimaryVideoStream;
        if (videoStream is null)
            return Error.Failure("VideoUploading.Infrastructure.Media","No Primary VideoStream for video" , new Dictionary<string, object>()
            {
                { "VideoId" ,video.Id}
            });

        return new VideoSpecifications(mediaInfo.Duration,
            new Resolution(videoStream.Width,videoStream.Height));
    }
}

