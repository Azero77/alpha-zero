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
        this.AccessControl("video:Stream", (req, tenantId) => ResourceArn.ForVideo(tenantId, req.VideoId));
        Description(d => d.WithTags("Video Streaming"));
    }

    public override async Task HandleAsync(GetVideoKeyRequest req, CancellationToken ct)
    {
        var result = await _module.Send(new GetVideoKeyQuery(req.VideoId), ct);

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
