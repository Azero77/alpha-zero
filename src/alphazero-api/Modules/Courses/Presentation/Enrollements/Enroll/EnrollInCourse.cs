using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollements.Enroll;

public record EnrollInCourseRequest
{
    public Guid StudentId { get; init; }
    public Guid CourseId { get; init; }
}

public record EnrollInCourseResponse(Guid EnrollmentId);

public class EnrollInCourseSummary : Summary<EnrollInCourseEndpoint>
{
    public EnrollInCourseSummary()
    {
        Summary = "Enrolls a student in a course";
        Description = "Creates a new enrollment record and initializes the progress bitmask for the student.";
        ExampleRequest = new EnrollInCourseRequest
        {
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid()
        };
        Response<EnrollInCourseResponse>(201, "Student successfully enrolled");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (Enrollement.StudentId, Enrollement.CourseId)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Enroll permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Course not found (Course.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Student is already enrolled in this course (Enrollment.Exists)");
    }
}

public class EnrollInCourseEndpoint : Endpoint<EnrollInCourseRequest, EnrollInCourseResponse>
{
    private readonly CoursesModule _module;

    public EnrollInCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/enroll");
        this.AccessControl("courses:Enroll", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Enrollment"));
        Summary(new EnrollInCourseSummary());
    }
    public override async Task HandleAsync(EnrollInCourseRequest req, CancellationToken ct)
    {
        var command = new EnrollInCourseCommand(req.StudentId, req.CourseId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"/courses/enrollments/{result.Value}",responseBody:  new EnrollInCourseResponse(result.Value),cancellation: ct);
    }
}
