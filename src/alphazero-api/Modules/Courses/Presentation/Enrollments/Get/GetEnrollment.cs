using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollments.Get;

public record GetEnrollmentRequest { public Guid Id { get; init; } }

public record EnrollmentResponse(
    Guid Id,
    Guid StudentId,
    Guid CourseId,
    string Status,
    double CompletionPercentage,
    DateTime EnrolledOn,
    Guid TenantId);

public class GetEnrollmentSummary : Summary<GetEnrollmentEndpoint>
{
    public GetEnrollmentSummary()
    {
        Summary = "Retrieves enrollment details";
        Description = "Returns the student enrollment details including completion percentage and status.";
        Response<EnrollmentResponse>(200, "Enrollment retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing enrollments:View permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Enrollment not found (Enrollment.NotFound)");
    }
}

public class GetEnrollmentEndpoint : Endpoint<GetEnrollmentRequest, EnrollmentResponse>
{
    private readonly CoursesModule _module;

    public GetEnrollmentEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/enrollments/{Id}");
        this.AccessControl("enrollments:View", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.Id));
        Description(d => d.WithTags("Enrollment"));
        Summary(new GetEnrollmentSummary());
    }

    public override async Task HandleAsync(GetEnrollmentRequest req, CancellationToken ct)
    {
        var query = new GetEnrollmentQuery(req.Id);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var enrollment = result.Value;
        var response = new EnrollmentResponse(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.Status,
            enrollment.CompletionPercentage,
            enrollment.EnrolledOn,
            enrollment.TenantId);

        await Send.OkAsync(response, ct);
    }
}
