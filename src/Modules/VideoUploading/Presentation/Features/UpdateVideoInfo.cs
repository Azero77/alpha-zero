using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Modules.VideoUploading.Application.Commands.Update;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.API.Shared;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public static class UpdateVideoInfo
{
    public record Request(string Title, string? Description);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPatch("api/video-uploading/debug/videos/{id:guid}", Handler)
               .WithTags("Video Uploading Debug")
               .AccessControl("video:Edit", ctx => ResourceArn.ForVideo(Guid.Empty, Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? Guid.Empty.ToString())));
        }

        private async Task<IResult> Handler(Guid id, Request request, VideoUploadingModule module)
        {
            var command = new UpdateVideoInfoCommand(id, request.Title, request.Description);
            var response = await module.Send<UpdateVideoInfoCommand, ErrorOr<Success>>(command);
            return response.Match(
                res => Results.NoContent(),
                errors => errors.ToMinimalResult());
        }
    }
}
