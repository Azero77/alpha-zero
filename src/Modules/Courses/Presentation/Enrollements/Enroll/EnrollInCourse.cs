using AlphaZero.Modules.Courses.Application.Enrollements.Commands.Enroll;
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
        Response(400, "Validation failure");
        Response(404, "Course not found");
        Response(409, "Student is already enrolled in this course");
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
        AllowAnonymous();
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

        await Send.CreatedAtAsync($"courses/enrollments/{result.Value}", ct);
    }
}
