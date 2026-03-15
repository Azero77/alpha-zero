using AlphaZero.API.Shared;
using AlphaZero.Shared.Presentation.Extensions;
using Application.Commands.Upload;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Presentation.Features;

public static class Upload
{
    public record Request(string fileName,string contentType);
    public record Response(string key,string preSignedUrl);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("api/courses/upload",Handler);
        }

        private async Task<IResult> Handler(Request request, IMediator mediatr)
        {
            var command = new UploadCommand(request.fileName,request.contentType);
            var response = await mediatr.Send(command);
            return response.Match(Results.Ok, errors => errors.ToMinimalResult());

        }
    }
}
