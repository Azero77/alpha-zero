using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetStudentDashboard;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollements.Dashboard;

public record GetStudentDashboardRequest { public Guid StudentId { get; init; } }

public record DashboardResponse(Dictionary<Guid, List<EnrollmentDto>> Academies);

public record EnrollmentDto(
    Guid EnrollmentId,
    Guid CourseId,
    string Status,
    double CompletionPercentage,
    DateTime EnrolledOn);

public class GetStudentDashboardSummary : Summary<GetStudentDashboardEndpoint>
{
    public GetStudentDashboardSummary()
    {
        Summary = "Retrieves student's learning dashboard across all academies";
        Description = "Returns a list of all active course enrollments for the student, grouped by the academy (tenant) they belong to.";
        Response<DashboardResponse>(200, "Dashboard retrieved successfully");
    }
}

public class GetStudentDashboardEndpoint : Endpoint<GetStudentDashboardRequest, DashboardResponse>
{
    private readonly CoursesModule _module;

    public GetStudentDashboardEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/dashboard/{StudentId}");
        AllowAnonymous();
        Description(d => d.WithTags("Enrollment"));
        Summary(new GetStudentDashboardSummary());
    }
    public override async Task HandleAsync(GetStudentDashboardRequest req, CancellationToken ct)
    {
        var query = new GetStudentDashboardQuery(req.StudentId);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var academies = result.Value.ToDictionary(
            g => g.Key,
            g => g.Value.Select(e => new EnrollmentDto(
                e.Id,
                e.CourseId,
                e.Status,
                e.CompletionPercentage,
                e.EnrolledOn)).ToList());

        await Send.OkAsync(new DashboardResponse(academies), ct);
    }
}
