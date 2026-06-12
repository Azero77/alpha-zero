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
