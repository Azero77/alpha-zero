using AlphaZero.Modules.Courses.Application.Courses.Commands.Sections;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Sections;

public record DeleteCourseSectionRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
}

public class DeleteCourseSectionSummary : Summary<DeleteCourseSectionEndpoint>
{
    public DeleteCourseSectionSummary()
    {
        Summary = "Soft-deletes a course section";
        Description = "Retires a section: its items become inactive; other sections' item ordering and bit indexes stay stable and student progress percentages recompute against the remaining active items. Earned completion is never lost.";
        Response(204, "Section deleted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (ids empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course or section not found (Course.NotFound, Course.Section)");
    }
}

public class DeleteCourseSectionEndpoint : Endpoint<DeleteCourseSectionRequest>
{
    private readonly CoursesModule _module;

    public DeleteCourseSectionEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/{CourseId}/sections/{SectionId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Sections"));
        Summary(new DeleteCourseSectionSummary());
    }

    public override async Task HandleAsync(DeleteCourseSectionRequest req, CancellationToken ct)
    {
        var command = new DeleteCourseSectionCommand(req.CourseId, req.SectionId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
