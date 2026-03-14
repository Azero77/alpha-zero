using AlphaZero.API.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks.Sources;

namespace Presentation.Features;

public static class RequestUpload
{
    public record Request();
    public record Response();
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/upload", Handler);
        }

        private IResult Handler(Request request)
        {
            return Results.Ok(new Response());
        }
    }
}
