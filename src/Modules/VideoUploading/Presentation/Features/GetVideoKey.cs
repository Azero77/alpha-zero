using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features
{
    public static class GetVideoKey
    {
        public class Endpoint : IEndpoint
        {
            public void MapEndpoint(IEndpointRouteBuilder app)
            {
                // This is the production-style Key Delivery Service (KDS)
                app.MapGet("api/video/keys/{videoId:guid}", Handler)
                   .WithTags("Video Streaming")
                   .Produces(StatusCodes.Status200OK, typeof(byte[]), "application/octet-stream");
            }

            private async Task<IResult> Handler(Guid videoId, VideoUploadingModule module)
            {
                // Cross-module request to VideoUploading to get the raw secret
                // In production, we would add [Authorize] and check course enrollment here
                var result = await module.Send<GetVideoKeyQuery, ErrorOr<byte[]>>(new GetVideoKeyQuery(videoId));

                return result.Match(
                    keyBytes => Results.File(keyBytes, "application/octet-stream"),
                    errors => Results.NotFound());
            }
        }
    }
}
