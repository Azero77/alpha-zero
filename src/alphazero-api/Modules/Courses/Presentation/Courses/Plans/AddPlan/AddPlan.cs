using AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Plans.AddPlan;

public record AddPlanRequest
{
    public Guid CourseId { get; init; }
    public string Name { get; init; } = default!;
    public Guid PrincipalId { get; init; }
}

public record AddPlanResponse(Guid PlanId);

public class AddPlanSummary : Summary<AddPlanEndpoint>
{
    public AddPlanSummary()
    {
        Summary = "Adds an access plan to a course";
        Description = "Creates a new plan option for accessing the course.";
        Response<AddPlanResponse>(200, "Plan added successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId, Name, PrincipalId empty or Course.PlanName)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
    }
}

public class AddPlanEndpoint : Endpoint<AddPlanRequest, AddPlanResponse>
{
    private readonly CoursesModule _module;

    public AddPlanEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/plans");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new AddPlanSummary());
    }

    public override async Task HandleAsync(AddPlanRequest req, CancellationToken ct)
    {
        var command = new AddPlanCommand(req.CourseId, req.Name, req.PrincipalId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new AddPlanResponse(result.Value), ct);
    }
}
