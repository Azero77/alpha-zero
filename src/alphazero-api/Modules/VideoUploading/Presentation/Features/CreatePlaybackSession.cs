using AlphaZero.Modules.VideoUploading.Application.Commands.CreatePlaybackSession;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public record CreatePlaybackSessionRequest
{
    public Guid VideoId { get; init; }
    public Guid? CourseId { get; init; }
    public Guid? ItemId { get; init; }
}

public class CreatePlaybackSessionSummary : Summary<CreatePlaybackSessionEndpoint>
{
    public CreatePlaybackSessionSummary()
    {
        Summary = "Initializes an authenticated video playback session";
        Description = "Validates user permissions/enrollment and issues a signed edge capability cookie for Cloudflare CDN segment delivery along with dynamic watermark identity context.";
        Response<PlaybackSessionDto>(StatusCodes.Status200OK, "Playback session initialized successfully.");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden, "Forbidden (Missing permission or not enrolled)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound, "Video not found");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict, "Video is not ready for playback");
    }
}

public class CreatePlaybackSessionEndpoint : Endpoint<CreatePlaybackSessionRequest, PlaybackSessionDto>
{
    private readonly VideoUploadingModule _module;

    public CreatePlaybackSessionEndpoint(VideoUploadingModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("api/videos/{VideoId:guid}/playback-session");
        this.AccessControl("video:Stream", (req, tenantId) =>
            req.CourseId.HasValue
                ? ResourceArn.ForCourseVideo(tenantId, req.VideoId, req.CourseId.Value)
                : ResourceArn.ForVideo(tenantId, req.VideoId));
        Description(d => d.WithTags("Video Streaming"));
        Summary(new CreatePlaybackSessionSummary());
    }

    public override async Task HandleAsync(CreatePlaybackSessionRequest req, CancellationToken ct)
    {
        var command = new CreatePlaybackSessionCommand(req.VideoId, req.CourseId, req.ItemId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
