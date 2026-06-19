namespace AlphaZero.Modules.VideoUploading.Application.Queries;

public interface IVideoQueryService
{
    Task<string?> GetVideoSecretKeyAsync(Guid videoId, CancellationToken cancellationToken = default);
}
