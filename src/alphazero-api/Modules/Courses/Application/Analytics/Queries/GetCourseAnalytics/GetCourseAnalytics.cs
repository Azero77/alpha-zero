using AlphaZero.Modules.Courses.Application.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Analytics.Queries.GetCourseAnalytics;

public record GetCourseAnalyticsQuery(Guid CourseId) : IRequest<ErrorOr<CourseAnalyticsDto>>;

public record CourseAnalyticsDto(
    Guid CourseId,
    int TotalEnrollments,
    double AverageCompletionPercentage,
    List<ItemCompletionDto> ItemCompletionRates);

public record ItemCompletionDto(
    int BitIndex,
    int CompletedCount,
    double CompletionPercentage);

public class GetCourseAnalyticsQueryHandler : IRequestHandler<GetCourseAnalyticsQuery, ErrorOr<CourseAnalyticsDto>>
{
    private readonly ICourseAnalyticsQueryService _queryService;

    public GetCourseAnalyticsQueryHandler(ICourseAnalyticsQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<ErrorOr<CourseAnalyticsDto>> Handle(GetCourseAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var analytics = await _queryService.GetCourseAnalyticsAsync(request.CourseId, cancellationToken);

        if (analytics == null)
            return Error.NotFound("CourseAnalytics.NotFound", "Analytics not found for this course.");

        return analytics;
    }
}
