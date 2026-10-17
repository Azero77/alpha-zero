using AlphaZero.Modules.Courses.Application.Courses.Commands.Items;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Items;

public record DeleteCourseSectionItemRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public Guid ItemId { get; init; }
}

public class DeleteCourseSectionItemSummary : Summary<DeleteCourseSectionItemEndpoint>
{
    public DeleteCourseSectionItemSummary()
    {
        Summary = "Deletes a curriculum section item";
        Description = "Soft deletes the section item, releases its asset references back to the pool, and permanently burns the bit index to preserve progress bitmasks.";
        Response(204, "Curriculum item deleted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course, section, or item not found");
    }
}

public class DeleteCourseSectionItemEndpoint : Endpoint<DeleteCourseSectionItemRequest>
{
    private readonly CoursesModule _module;

    public DeleteCourseSectionItemEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/{CourseId}/sections/{SectionId}/items/{ItemId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new DeleteCourseSectionItemSummary());
    }

    public override async Task HandleAsync(DeleteCourseSectionItemRequest req, CancellationToken ct)
    {
        var command = new DeleteCourseSectionItemCommand(
            req.CourseId,
            req.SectionId,
            req.ItemId);

        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
