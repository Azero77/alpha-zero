using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Modules.VideoUploading.Application.Commands.Upload;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using AlphaZero.API.Shared;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public static class Upload
{
    public record Request(string fileName, string contentType, string title, string? description);
    public record Response(Guid videoId, Guid tenantId, string key, string preSignedUrl);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("api/video-uploading/upload", Handler)
               .WithTags("Video Uploading")
               .AccessControl("video:Upload", _ => ResourceArn.ForTenant(Guid.Empty));
        }

        private async Task<IResult> Handler(Request request, VideoUploadingModule module)
        {
            var command = new UploadCommand(request.fileName, request.contentType, request.title, request.description);
            var response = await module.Send<UploadCommand, ErrorOr<UploadCommandResponse>>(command);
            return response.Match(
                res => Results.Ok(new Response(res.VideoId, res.TenantId, res.Key, res.PreSignedUrl)),
                errors => errors.ToMinimalResult());
        }
    }}
