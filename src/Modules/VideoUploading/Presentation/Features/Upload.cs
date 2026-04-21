using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Modules.VideoUploading.Application.Commands.Upload;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoUploading.Application;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public static class Upload
{
    public record Request(
        string fileName, 
        string contentType, 
        string title, 
        string? description,
        VideoTranscodingMetehod? transcodingMethod,
        VideoEncryptionMethod? encryptionMethod);
    public record Response(
        Guid videoId, 
        Guid tenantId, 
        string key, 
        string preSignedUrl,
        string transcodingMethod,
        string encryptionMethod);

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
            var transcodingMethod = request.transcodingMethod ?? VideoTranscodingMetehod.FFMPEG;
            var encryptionMethod = request.encryptionMethod ?? VideoEncryptionMethod.ClearKey;

            var command = new UploadCommand(
                request.fileName, 
                request.contentType, 
                request.title, 
                request.description, 
                transcodingMethod,
                encryptionMethod);
            var response = await module.Send<UploadCommand, ErrorOr<UploadCommandResponse>>(command);
            return response.Match(
                res => Results.Ok(new Response(
                    res.VideoId, 
                    res.TenantId, 
                    res.Key, 
                    res.PreSignedUrl,
                    res.TranscodingMethod,
                    res.EncryptionMethod)),
                errors => errors.ToMinimalResult());
        }
    }}
