using AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Plans.RemovePlan;

public record RemovePlanRequest
{
    public Guid CourseId { get; init; }
    public Guid PlanId { get; init; }
}

public class RemovePlanSummary : Summary<RemovePlanEndpoint>
{
    public RemovePlanSummary()
    {
        Summary = "Removes an access plan from a course";
        Description = "Deletes an enrollment plan option from the specified course.";
        Response(204, "Plan removed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId or PlanId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Course.NotFound, Course.Plan)");
    }
}

public class RemovePlanEndpoint : Endpoint<RemovePlanRequest>
{
    private readonly CoursesModule _module;

    public RemovePlanEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/{CourseId}/plans/{PlanId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new RemovePlanSummary());
    }

    public override async Task HandleAsync(RemovePlanRequest req, CancellationToken ct)
    {
        var command = new RemovePlanCommand(req.CourseId, req.PlanId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
