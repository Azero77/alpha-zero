using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoUploading.Application.Streaming.Queries;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features.Streaming;

public static class GetStreamingInfo
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video/{videoId:guid}", Handler)
               .WithTags("Video Streaming")
               .WithSummary("Gets streaming playback information for a video")
               .WithDescription("Retrieves the HLS/DASH manifest URL and DRM/encryption licensing information.")
               .Produces<StreamingInfoResponseDTO>(StatusCodes.Status200OK)
               .ProducesProblem(StatusCodes.Status404NotFound);
        }
        private async Task<IResult> Handler(Guid videoId, VideoUploadingModule module)
        {
            var result = await module.Send<GetStreaminInfoForVideoQuery, ErrorOr<StreamingInfoResponseDTO>>(new GetStreaminInfoForVideoQuery(videoId));

            return result.Match(res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }
}
