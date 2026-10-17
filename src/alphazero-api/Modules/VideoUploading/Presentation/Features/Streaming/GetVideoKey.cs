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

[Obsolete("Deprecated. Use GET api/video/keys/{VideoId:guid} (AlphaZero.Modules.VideoUploading.Presentation.Features.GetVideoKeyEndpoint) instead.")]
public static class GetVideoKey
{
    public record Request
    {
        public Guid VideoId { get; init; }
        [QueryParam]
        public string? Ticket { get; init; }
        [FromHeader("X-Playback-Ticket", isRequired: false)]
        public string? HeaderTicket { get; init; }
    }

    public class Endpoint(VideoUploadingModule module) : Endpoint<Request>
    {
        public override void Configure()
        {
            Get("api/videos/{videoId:guid}/key");

            Tags("Video Streaming");

            Summary(s =>
            {
                s.Summary = "Gets the ClearKey decryption key for a video [Deprecated]";
                s.Description = "Deprecated. Use GET api/video/keys/{VideoId:guid} instead.";
            });

            Description(d =>
            {
                d.Produces<byte[]>(
                    StatusCodes.Status200OK,
                    "application/octet-stream");

                d.ProducesProblem(StatusCodes.Status404NotFound);
            });
        }

        public override async Task HandleAsync(
            Request req,
            CancellationToken ct)
        {
            var ticket = !string.IsNullOrWhiteSpace(req.Ticket) ? req.Ticket : req.HeaderTicket;
            var result = await module.Send(
                new AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey.GetVideoKeyQuery(req.VideoId, ticket), ct);

            if (result.IsError)
            {
                await this.SendErrorResponseAsync(result.Errors, ct);
                return;
            }

            HttpContext.Response.ContentType = "application/octet-stream";
            HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            await HttpContext.Response.Body.WriteAsync(result.Value, ct);
        }
    }
}
