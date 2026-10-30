using AlphaZero.Modules.Courses.Application.Courses.Commands.Update;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Update;

public record UpdateCourseRequest
{
    public Guid CourseId { get; init; }
    public string Title { get; init; } = default!;
    public string? Description { get; init; }
    public Guid SubjectId { get; init; }
}

public class UpdateCourseSummary : Summary<UpdateCourseEndpoint>
{
    public UpdateCourseSummary()
    {
        Summary = "Updates a course";
        Description = "Updates title, description and subject reassignment of a course. Permitted in any lifecycle state; no status change occurs.";
        ExampleRequest = new UpdateCourseRequest
        {
            CourseId = Guid.NewGuid(),
            Title = "Introduction to Applied Algebra",
            Description = "A refined pitch for the course.",
            SubjectId = Guid.NewGuid()
        };
        Response(204, "Course updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Course title empty/too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course or subject not found (Course.NotFound, Course.SubjectId)");
    }
}

public class UpdateCourseEndpoint : Endpoint<UpdateCourseRequest>
{
    private readonly CoursesModule _module;

    public UpdateCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/courses/{CourseId}");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d
            .WithTags("Courses")
            .Accepts<UpdateCourseRequest>("application/json"));
        Summary(new UpdateCourseSummary());
    }

    public override async Task HandleAsync(UpdateCourseRequest req, CancellationToken ct)
    {
        var command = new UpdateCourseCommand(req.CourseId, req.Title, req.Description, req.SubjectId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
