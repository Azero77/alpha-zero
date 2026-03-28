using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Application.Services;

public interface IVideoTranscodingService
{
    Task<ErrorOr<string>> StartTranscodingJobAsync(
        Guid videoId, 
        string inputS3Uri, 
        string outputPathS3Uri, 
        CancellationToken cancellationToken = default);
}