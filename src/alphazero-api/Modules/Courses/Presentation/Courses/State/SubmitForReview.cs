using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record SubmitForReviewRequest
{
    public Guid CourseId { get; init; }
}

public class SubmitForReviewSummary : Summary<SubmitForReviewEndpoint>
{
    public SubmitForReviewSummary()
    {
        Summary = "Submits a course for administrative review";
        Description = "Transitions course status from Draft to UnderReview.";
        Response(204, "Course submitted for review successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Course.Empty - course must have content before review)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Submit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Course.Status - only draft courses can be reviewed)");
    }
}

public class SubmitForReviewEndpoint : Endpoint<SubmitForReviewRequest>
{
    private readonly CoursesModule _module;

    public SubmitForReviewEndpoint(CoursesModule module)
    {
        _module = module;
    }
    public override void Configure()
    {
        Patch("/courses/{CourseId}/review");
        this.AccessControl("courses:Submit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d
            .WithTags("Courses")
            .Accepts<SubmitForReviewRequest>("application/json"));
        Summary(new SubmitForReviewSummary());
    }

    public override async Task HandleAsync(SubmitForReviewRequest req, CancellationToken ct)
    {
        var command = new SubmitCourseForReviewCommand(req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
