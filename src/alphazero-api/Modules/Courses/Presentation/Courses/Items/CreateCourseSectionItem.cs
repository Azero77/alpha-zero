using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Items;

public record CreateCourseSectionItemRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public string MainType { get; init; } = default!;
    public JsonElement Content { get; init; }
}

public class CreateCourseSectionItemSummary : Summary<CreateCourseSectionItemEndpoint>
{
    public CreateCourseSectionItemSummary()
    {
        Summary = "Creates a curriculum section item";
        Description = "Appends a composite content item (Video, Document, Article, Quiz) to a section and manages asset ref counts.";
        Response<CurriculumItemDto>(201, "Curriculum item created successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Invalid MainType or invalid JSON structure)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course, section, or referenced asset not found");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Referenced asset not available in course pool)");
    }
}

public class CreateCourseSectionItemEndpoint : Endpoint<CreateCourseSectionItemRequest, CurriculumItemDto>
{
    private readonly CoursesModule _module;

    public CreateCourseSectionItemEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/items");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new CreateCourseSectionItemSummary());
    }

    public override async Task HandleAsync(CreateCourseSectionItemRequest req, CancellationToken ct)
    {
        var command = new CreateCourseSectionItemCommand(
            req.CourseId,
            req.SectionId,
            req.Title,
            req.MainType,
            req.Content);

        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync(
            $"/courses/{req.CourseId}/sections/{req.SectionId}/items/{result.Value.Id}",
            responseBody: result.Value,
            cancellation: ct);
    }
}
