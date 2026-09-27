namespace AlphaZero.Modules.VideoUploading.Application.Services;

public record VideoProgressNotification(
    string VideoId,
    string TenantId,
    string Stage,
    string Status,
    string? Metadata);

public interface IVideoProgressNotifier
{
    Task NotifyProgressAsync(VideoProgressNotification notification, CancellationToken cancellationToken = default);
}
