using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record RejectCourseRequest
{
    public Guid CourseId { get; init; }
    public string Reason { get; init; } = default!;
}

public class RejectCourseSummary : Summary<RejectCourseEndpoint>
{
    public RejectCourseSummary()
    {
        Summary = "Rejects a course under review";
        Description = "Transitions course status from UnderReview back to Draft with a specified rejection reason.";
        Response(204, "Course rejected successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Course.RejectionReason - rejection reason is required)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Reject permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Course.Status - only courses under review can be rejected)");
    }
}

public class RejectCourseEndpoint : Endpoint<RejectCourseRequest>
{
    private readonly CoursesModule _module;

    public RejectCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Patch("/courses/{CourseId}/reject");
        this.AccessControl("courses:Reject", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new RejectCourseSummary());
    }

    public override async Task HandleAsync(RejectCourseRequest req, CancellationToken ct)
    {
        var command = new RejectCourseCommand(req.CourseId, req.Reason);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
