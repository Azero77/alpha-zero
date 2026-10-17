using AlphaZero.Modules.Courses.Application.Courses.Commands.Delete;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Delete;

public record DeleteCourseRequest
{
    public Guid CourseId { get; init; }
}

public class DeleteCourseSummary : Summary<DeleteCourseEndpoint>
{
    public DeleteCourseSummary()
    {
        Summary = "Soft-deletes a course";
        Description = "Removes the course from every list/read endpoint via the global query filter while retaining the row (and enrollments/audit data) for audit purposes.";
        Response(204, "Course deleted successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (CourseId empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
    }
}

public class DeleteCourseEndpoint : Endpoint<DeleteCourseRequest>
{
    private readonly CoursesModule _module;

    public DeleteCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Delete("/courses/{CourseId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new DeleteCourseSummary());
    }

    public override async Task HandleAsync(DeleteCourseRequest req, CancellationToken ct)
    {
        var command = new DeleteCourseCommand(req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
