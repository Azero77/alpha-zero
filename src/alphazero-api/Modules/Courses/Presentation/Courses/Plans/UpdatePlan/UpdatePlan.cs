using AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Plans.UpdatePlan;

public record UpdatePlanRequest
{
    public Guid CourseId { get; init; }
    public Guid PlanId { get; init; }
    public string Name { get; init; } = default!;
    public Guid PrincipalId { get; init; }
}

public class UpdatePlanSummary : Summary<UpdatePlanEndpoint>
{
    public UpdatePlanSummary()
    {
        Summary = "Updates an access plan for a course";
        Description = "Updates the plan name or associated principal for accessing the course.";
        Response(204, "Plan updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId, PlanId, Name, PrincipalId empty or Course.PlanName)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Course.NotFound, Course.Plan)");
    }
}

public class UpdatePlanEndpoint : Endpoint<UpdatePlanRequest>
{
    private readonly CoursesModule _module;

    public UpdatePlanEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/courses/{CourseId}/plans/{PlanId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new UpdatePlanSummary());
    }

    public override async Task HandleAsync(UpdatePlanRequest req, CancellationToken ct)
    {
        var command = new UpdatePlanCommand(req.CourseId, req.PlanId, req.Name, req.PrincipalId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
