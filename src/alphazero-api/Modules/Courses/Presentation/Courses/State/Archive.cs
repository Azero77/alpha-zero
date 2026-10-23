using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.State;

public record ArchiveCourseRequest
{
    public Guid CourseId { get; init; }
}

public class ArchiveCourseSummary : Summary<ArchiveCourseEndpoint>
{
    public ArchiveCourseSummary()
    {
        Summary = "Archives a course";
        Description = "Retires a course from circulation while retaining its history and enrollments. Archived courses keep their data; deletion is the stronger (soft) removal.";
        Response(204, "Course archived successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Course.Status - course is already archived)");
    }
}

public class ArchiveCourseEndpoint : Endpoint<ArchiveCourseRequest>
{
    private readonly CoursesModule _module;

    public ArchiveCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Patch("/courses/{CourseId}/archive");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new ArchiveCourseSummary());
    }

    public override async Task HandleAsync(ArchiveCourseRequest req, CancellationToken ct)
    {
        var command = new ArchiveCourseCommand(req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
