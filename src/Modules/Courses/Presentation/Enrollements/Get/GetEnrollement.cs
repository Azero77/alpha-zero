using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollements.Get;

public record GetEnrollementRequest { public Guid Id { get; init; } }

public record EnrollementResponse(
    Guid Id,
    Guid StudentId,
    Guid CourseId,
    string Status,
    double CompletionPercentage,
    DateTime EnrolledOn,
    Guid TenantId);

public class GetEnrollementEndpoint : Endpoint<GetEnrollementRequest, EnrollementResponse>
{
    private readonly CoursesModule _module;

    public GetEnrollementEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/enrollments/{Id}");
        AllowAnonymous();
        Description(d => d.WithTags("Enrollment"));
    }

    public override async Task HandleAsync(GetEnrollementRequest req, CancellationToken ct)
    {
        var query = new GetEnrollementQuery(req.Id);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var enrollment = result.Value;
        var response = new EnrollementResponse(
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
