using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Items;

public record UpdateCourseSectionItemRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public Guid ItemId { get; init; }
    public string Title { get; init; } = default!;
    public string MainType { get; init; } = default!;
    public JsonElement Content { get; init; }
}

public class UpdateCourseSectionItemSummary : Summary<UpdateCourseSectionItemEndpoint>
{
    public UpdateCourseSectionItemSummary()
    {
        Summary = "Updates a curriculum section item";
        Description = "Updates the composite content blocks and reconciles asset ref counts atomically without modifying BitIndex.";
        Response<CurriculumItemDto>(200, "Curriculum item updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Invalid MainType or invalid JSON structure)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course, section, item, or referenced asset not found");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Referenced asset not available in course pool)");
    }
}

public class UpdateCourseSectionItemEndpoint : Endpoint<UpdateCourseSectionItemRequest, CurriculumItemDto>
{
    private readonly CoursesModule _module;

    public UpdateCourseSectionItemEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/courses/{CourseId}/sections/{SectionId}/items/{ItemId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new UpdateCourseSectionItemSummary());
    }

    public override async Task HandleAsync(UpdateCourseSectionItemRequest req, CancellationToken ct)
    {
        var command = new UpdateCourseSectionItemCommand(
            req.CourseId,
            req.SectionId,
            req.ItemId,
            req.Title,
            req.MainType,
            req.Content);

        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
