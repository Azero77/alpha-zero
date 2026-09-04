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
