using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Pool;

public record DismissAssetRequest
{
    public Guid CourseId { get; init; }
    public Guid AssetId { get; init; }
}

public class DismissAssetSummary : Summary<DismissAssetEndpoint>
{
    public DismissAssetSummary()
    {
        Summary = "Dismisses an unused asset from the course pool";
        Description = "Transitions the asset state to Archived, removing it from the course pool.";
        Response(204, "Asset dismissed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Asset not found");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Cannot dismiss an asset currently in use");
    }
}

public class DismissAssetEndpoint : Endpoint<DismissAssetRequest>
{
    private readonly CoursesModule _module;

    public DismissAssetEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/{CourseId}/pool/{AssetId}/dismiss");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new DismissAssetSummary());
    }

    public override async Task HandleAsync(DismissAssetRequest req, CancellationToken ct)
    {
        var command = new DismissAssetCommand(req.CourseId, req.AssetId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
