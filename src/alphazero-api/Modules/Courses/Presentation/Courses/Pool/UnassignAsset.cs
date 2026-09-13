using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Pool;

public record UnassignAssetRequest
{
    public Guid CourseId { get; init; }
    public Guid ItemId { get; init; }
}

public class UnassignAssetSummary : Summary<UnassignAssetEndpoint>
{
    public UnassignAssetSummary()
    {
        Summary = "Unassigns an item from the curriculum and returns its assets to the pool";
        Description = "Removes the curriculum item and transitions all attached assets back to Available state in the course pool.";
        Response(204, "Item unassigned successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course or item not found");
    }
}

public class UnassignAssetEndpoint : Endpoint<UnassignAssetRequest>
{
    private readonly CoursesModule _module;

    public UnassignAssetEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/{CourseId}/items/{ItemId}/unassign");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new UnassignAssetSummary());
    }

    public override async Task HandleAsync(UnassignAssetRequest req, CancellationToken ct)
    {
        var command = new UnassignAssetFromCurriculumCommand(req.CourseId, req.ItemId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
