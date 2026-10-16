using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoKey;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public record GetVideoKeyRequest
{
    public Guid VideoId { get; init; }
    [QueryParam]
    public string? Ticket { get; init; }
    [FromHeader("X-Playback-Ticket", isRequired: false)]
    public string? HeaderTicket { get; init; }
}

public class GetVideoKeySummary : Summary<GetVideoKeyEndpoint>
{
    public GetVideoKeySummary()
    {
        Summary = "Retrieves decryption key for an encrypted video";
        Description = "Returns the binary 16-byte AES decryption key for HLS playback.";
        Response(200, "Decryption key binary stream (application/octet-stream)", "application/octet-stream");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing video:Stream permission or invalid ticket)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Video secret not found (VideoSecret.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(500, "Invalid key format stored (VideoSecret.InvalidFormat)");
    }
}

public class GetVideoKeyEndpoint : Endpoint<GetVideoKeyRequest>
{
    private readonly VideoUploadingModule _module;

    public GetVideoKeyEndpoint(VideoUploadingModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("api/video/keys/{VideoId:guid}");
        Description(d => d.WithTags("Video Streaming"));
        Summary(new GetVideoKeySummary());
    }

    public override async Task HandleAsync(GetVideoKeyRequest req, CancellationToken ct)
    {
        var ticket = !string.IsNullOrWhiteSpace(req.Ticket) ? req.Ticket : req.HeaderTicket;
        var result = await _module.Send(new GetVideoKeyQuery(req.VideoId, ticket), ct);

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
