using AlphaZero.API.Shared;
using AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession;
using AlphaZero.Modules.VideoUploading.Application.Streaming.Queries;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features.Streaming;

public static class GetVideoKey
{
    public record Request(Guid VideoId);

    public class Endpoint(VideoUploadingModule module) : Endpoint<Request>
    {
        public override void Configure()
        {
            Get("api/videos/{videoId:guid}/key");

            Tags("Video Streaming");

            Summary(s =>
            {
                s.Summary = "Gets the ClearKey decryption key for a video";
            });

            Description(d =>
            {
                d.Produces<byte[]>(
                    StatusCodes.Status200OK,
                    "application/octet-stream");

                d.ProducesProblem(StatusCodes.Status404NotFound);
            });
            this.AccessControl("video:Stream", (req, tenantId) => ResourceArn.ForVideo(tenantId, req.VideoId));
        }

        public override async Task HandleAsync(
            Request req,
            CancellationToken ct)
        {
            var result = await module.Send(
                new GetVideoKeyQuery(req.VideoId));

            await result.Match(
                async keyBytes =>
                {
                    HttpContext.Response.ContentType = "application/octet-stream";
                    await HttpContext.Response.Body.WriteAsync(keyBytes, ct);
                },
                async errors =>
                {
                    await this.SendErrorResponseAsync(result.Errors, ct);
                });
        }
    }
}
