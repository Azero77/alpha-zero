using AlphaZero.Modules.Courses.Application.Courses.Commands.Sections;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Sections;

public record RenameCourseSectionRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
}

public class RenameCourseSectionSummary : Summary<RenameCourseSectionEndpoint>
{
    public RenameCourseSectionSummary()
    {
        Summary = "Renames a course section";
        Description = "Renames a section in place; curriculum items and their completion bit indexes remain unchanged.";
        ExampleRequest = new RenameCourseSectionRequest
        {
            CourseId = Guid.NewGuid(),
            SectionId = Guid.NewGuid(),
            Title = "Week 1 — Foundations"
        };
        Response(204, "Section renamed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (title empty/too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course or section not found (Course.NotFound, Course.Section)");
    }
}

public class RenameCourseSectionEndpoint : Endpoint<RenameCourseSectionRequest>
{
    private readonly CoursesModule _module;

    public RenameCourseSectionEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/courses/{CourseId}/sections/{SectionId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d
            .WithTags("Sections")
            .Accepts<RenameCourseSectionRequest>("application/json"));
        Summary(new RenameCourseSectionSummary());
    }

    public override async Task HandleAsync(RenameCourseSectionRequest req, CancellationToken ct)
    {
        var command = new RenameCourseSectionCommand(req.CourseId, req.SectionId, req.Title);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
