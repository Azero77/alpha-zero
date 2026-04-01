using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoStreaming.Application.Queries;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.Features;

public static class GetVideo
{
    public class Endpoint : IEndpoint
    {
        public record Response(string presignedUrl,string Key);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video/{videoId:guid}", Handler);
        }
        private async Task<IResult> Handler(Guid videoId, VideoStreamingModule module)
        {
            var result  = 
                await module.Send<GetStreaminInfoForVideoQuery,ErrorOr<StreamingInfoResponseDTO>>(new GetStreaminInfoForVideoQuery(videoId));

            return result.Match(res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }
}