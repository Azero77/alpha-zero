using Microsoft.AspNetCore.SignalR;

namespace AlphaZero.Modules.VideoUploading.Presentation.Hubs;

public class VideoProgressHub : Hub
{
    public async Task JoinVideoGroup(string videoId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"video-{videoId}");
    }

    public async Task LeaveVideoGroup(string videoId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"video-{videoId}");
    }
}
