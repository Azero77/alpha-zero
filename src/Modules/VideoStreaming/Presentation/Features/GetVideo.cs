using AlphaZero.API.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Presentation.Features;

public static class GetVideo
{
    public class Endpoint : IEndpoint
    {
        public record Response(string presignedUrl);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video/{videoId:guid}", Handler);
        }
        public IResult Handler(Guid videoId)
        {
            throw new NotImplementedException();
        }
    }
}