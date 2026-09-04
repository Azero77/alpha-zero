using AlphaZero.Modules.VideoUploading.Domain.Models;
using ErrorOr;

namespace AlphaZero.Modules.VideoUploading.Domain.Services;

public interface IVideoSpecificationExtractorService
{
    Task<ErrorOr<VideoSpecifications>> ExtractAsync(Video video, CancellationToken token = default);
    Task<ErrorOr<VideoSpecifications>> ExtractAsync(string sourceKey, CancellationToken token = default);
}
