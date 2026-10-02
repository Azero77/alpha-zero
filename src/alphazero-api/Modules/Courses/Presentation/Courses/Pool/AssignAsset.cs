using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Pool;

public record AssignAssetRequest
{
    public Guid CourseId { get; init; }
    public Guid AssetId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public JsonElement? Metadata { get; init; }
}

public record AssignAssetResponse(Guid ItemId);

public class AssignAssetSummary : Summary<AssignAssetEndpoint>
{
    public AssignAssetSummary()
    {
        Summary = "Assigns an available pool asset to a curriculum section";
        Description = "Creates a new curriculum item from an available course pool asset and transitions the asset state to InUse.";
        Response<AssignAssetResponse>(200, "Asset assigned to curriculum successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course, section, or asset not found");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Asset not in Available state");
    }
}

public class AssignAssetEndpoint : Endpoint<AssignAssetRequest, AssignAssetResponse>
{
    private readonly CoursesModule _module;

    public AssignAssetEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/pool/{AssetId}/assign");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new AssignAssetSummary());
    }

    public override async Task HandleAsync(AssignAssetRequest req, CancellationToken ct)
    {
        var command = new AssignAssetToCurriculumCommand(
            req.CourseId,
            req.SectionId,
            req.AssetId,
            req.Title,
            req.Metadata);

        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new AssignAssetResponse(result.Value), ct);
    }
}
