using AlphaZero.Modules.Courses.Application.Analytics.Queries.ListStudentProgress;
using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Analytics;

public record ListStudentProgressRequest 
{ 
    public Guid CourseId { get; init; } 
    public int Page { get; init; } = 1;
    public int PerPage { get; init; } = 10;
}

public class ListStudentProgressSummary : Summary<ListStudentProgressEndpoint>
{
    public ListStudentProgressSummary()
    {
        Summary = "Lists student progress for a course";
        Description = "Returns a paginated list of enrollments and their completion percentages.";
        Response<PagedResult<EnrollmentDto>>(200, "Progress list retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:ViewAnalytics permission)");
    }
}

public class ListStudentProgressEndpoint : Endpoint<ListStudentProgressRequest, PagedResult<EnrollmentDto>>
{
    private readonly CoursesModule _module;

    public ListStudentProgressEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/{CourseId}/students");
        this.AccessControl("courses:ViewAnalytics", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Analytics"));
        Summary(new ListStudentProgressSummary());
    }

    public override async Task HandleAsync(ListStudentProgressRequest req, CancellationToken ct)
    {
        var result = await _module.Send(new ListStudentProgressQuery(req.CourseId, req.Page, req.PerPage), ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
