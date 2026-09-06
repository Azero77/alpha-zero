using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Reorder.Sections;

public record ReorderSectionsRequest
{
    public Guid CourseId { get; init; }
    public List<Guid> SectionIds { get; init; } = new();
}

public class ReorderSectionsSummary : Summary<ReorderSectionsEndpoint>
{
    public ReorderSectionsSummary()
    {
        Summary = "Reorders sections within a course";
        Description = "Updates the sequence order of curriculum sections for a draft course.";
        Response(204, "Sections reordered successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Course.Status - cannot reorder sections once published)");
    }
}

public class ReorderSectionsEndpoint : Endpoint<ReorderSectionsRequest>
{
    private readonly CoursesModule _module;

    public ReorderSectionsEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/reorder");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new ReorderSectionsSummary());
    }

    public override async Task HandleAsync(ReorderSectionsRequest req, CancellationToken ct)
    {
        var command = new ReorderSectionsCommand(req.CourseId, req.SectionIds);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
