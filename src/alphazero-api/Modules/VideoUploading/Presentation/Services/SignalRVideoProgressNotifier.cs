using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Presentation.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AlphaZero.Modules.VideoUploading.Presentation.Services;

internal abstract class SignalRVideoProgressNotifier(IHubContext<VideoProgressHub> hubContext) : IVideoProgressNotifier
{
    public async Task NotifyProgressAsync(VideoProgressNotification notification, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.Group($"video-{notification.VideoId}")
            .SendAsync("ProgressUpdated", notification, cancellationToken);
    }
}
