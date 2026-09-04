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

public class AddPlanEndpoint : Endpoint<AddPlanRequest>
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

        await Send.OkAsync(new { PlanId = result.Value }, ct);
    }
}
