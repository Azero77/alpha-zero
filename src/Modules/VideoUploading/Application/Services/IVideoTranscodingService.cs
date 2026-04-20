using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Services;

public interface IVideoTranscodingService
{
    VideoTranscodingMetehod Method { get; }
    Task<ErrorOr<string>> StartTranscodingJobAsync(
        Guid videoId, 
        string inputS3Uri, 
        string outputPathS3Uri, 
        int sourceWidth,
        int sourceHeight,
        CancellationToken cancellationToken = default);
}
