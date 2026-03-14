using AlphaZero.API.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Presentation.Features;

public static class GetVideo
{
    public class Endpoint : IEndpoint
    {
        public record Request(Guid videoId);
        public record Response(string presignedUrl);
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video/{videoId:guid}", Handler);
        }
        public IResult Handler(Request request)
        {
            throw new NotImplementedException();
        }
    }
}