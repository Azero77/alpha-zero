using AlphaZero.Modules.Courses.Application.Courses.Commands.Playback;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Items;

public record IssuePlaybackTicketRequest
{
    public Guid ResourceId { get; init; }
    public Guid CourseId { get; init; }
}

public record IssuePlaybackTicketResponse(string Ticket, Guid VideoId, int ExpiresInSeconds);

public class IssuePlaybackTicketSummary : Summary<IssuePlaybackTicketEndpoint>
{
    public IssuePlaybackTicketSummary()
    {
        Summary = "Issues a capability-based playback ticket";
        Description = "Validates student enrollment and returns a short-lived HMAC-signed playback ticket for video streaming.";
        Response<IssuePlaybackTicketResponse>(200, "Playback ticket successfully issued");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Curriculum item does not contain a video resource");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:View permission or not enrolled)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course, section, or item not found");
    }
}

public class IssuePlaybackTicketEndpoint : Endpoint<IssuePlaybackTicketRequest, IssuePlaybackTicketResponse>
{
    private readonly CoursesModule _module;

    public IssuePlaybackTicketEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId:guid}/sections/{SectionId:guid}/items/{ItemId:guid}/playback-ticket");
        this.AccessControl("courses:View", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new IssuePlaybackTicketSummary());
    }

    public override async Task HandleAsync(IssuePlaybackTicketRequest req, CancellationToken ct)
    {
        var tenantUser = User.ToTenantUserDTO();
        var studentId = tenantUser?.UserId;

        if (studentId is null || studentId == Guid.Empty)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var command = new IssuePlaybackTicketCommand(req.CourseId, req.ResourceId);

        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new IssuePlaybackTicketResponse(
            result.Value.Ticket,
            result.Value.VideoId,
            result.Value.ExpiresInSeconds), ct);
    }
}
