using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record ApproveCourseRequest
{
    public Guid CourseId { get; init; }
}

public class ApproveCourseSummary : Summary<ApproveCourseEndpoint>
{
    public ApproveCourseSummary()
    {
        Summary = "Approves a course under review";
        Description = "Transitions course status from UnderReview to Approved.";
        Response(204, "Course approved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Approve permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Course.Status - only courses under review can be approved)");
    }
}

public class ApproveCourseEndpoint : Endpoint<ApproveCourseRequest>
{
    private readonly CoursesModule _module;

    public ApproveCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }
    public override void Configure()
    {
        Patch("/courses/{CourseId}/approve");
        this.AccessControl("courses:Approve", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d
            .WithTags("Courses")
            .Accepts<ApproveCourseRequest>("application/json"));
        Summary(new ApproveCourseSummary());
    }

    public override async Task HandleAsync(ApproveCourseRequest req, CancellationToken ct)
    {
        var command = new ApproveCourseCommand(req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
