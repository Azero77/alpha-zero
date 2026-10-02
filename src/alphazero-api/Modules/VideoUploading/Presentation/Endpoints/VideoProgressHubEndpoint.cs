using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoUploading.Presentation.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Endpoints;

public class VideoProgressHubEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapHub<VideoProgressHub>("/hubs/video-progress");
    }
}
