using AlphaZero.Modules.VideoUploading.Application.Services;
using AlphaZero.Modules.VideoUploading.Presentation.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AlphaZero.Modules.VideoUploading.Presentation.Services;

public class SignalRVideoProgressNotifier : IVideoProgressNotifier
{
    private readonly IHubContext<VideoProgressHub> _hubContext;

    public SignalRVideoProgressNotifier(IHubContext<VideoProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyProgressAsync(VideoProgressNotification notification, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"video-{notification.VideoId}")
            .SendAsync("ProgressUpdated", notification, cancellationToken);
    }
}
