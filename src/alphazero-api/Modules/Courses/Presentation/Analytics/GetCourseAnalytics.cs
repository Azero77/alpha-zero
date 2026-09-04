using AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Analytics;

public record GetCourseAnalyticsRequest { public Guid CourseId { get; init; } }

public class GetCourseAnalyticsSummary : Summary<GetCourseAnalyticsEndpoint>
{
    public GetCourseAnalyticsSummary()
    {
        Summary = "Retrieves analytics for a course";
        Description = "Returns total enrollments, average completion rate, and per-item completion stats.";
        Response<CourseAnalyticsDto>(200, "Analytics retrieved successfully");
    }
}

public class GetCourseAnalyticsEndpoint : Endpoint<GetCourseAnalyticsRequest, CourseAnalyticsDto>
{
    private readonly CoursesModule _module;

    public GetCourseAnalyticsEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/{CourseId}/analytics");
        this.AccessControl("courses:ViewAnalytics", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Analytics"));
        Summary(new GetCourseAnalyticsSummary());
    }

    public override async Task HandleAsync(GetCourseAnalyticsRequest req, CancellationToken ct)
    {
        var result = await _module.Send(new GetCourseAnalyticsQuery(req.CourseId), ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
