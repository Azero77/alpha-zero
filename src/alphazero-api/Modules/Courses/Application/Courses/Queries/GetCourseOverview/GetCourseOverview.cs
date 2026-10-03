using ErrorOr;
using MediatR;
using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;

public record CourseOverviewDto(
    Guid CourseId,
    string CourseStatus,
    JsonDocument DescriptionContent,
    JsonDocument? TargetAudienceContent,
    JsonDocument? LearningObjectivesContent,
    Guid? CoverImageDocumentId,
    string ETag);

public record GetCourseOverviewQuery(Guid CourseId) : IRequest<ErrorOr<CourseOverviewDto>>;

public class GetCourseOverviewQueryHandler : IRequestHandler<GetCourseOverviewQuery, ErrorOr<CourseOverviewDto>>
{
    private readonly ICourseOverviewRepository _courseOverviewRepository;
    private readonly ICourseQueryService _courseQueryService;

    public GetCourseOverviewQueryHandler(
        ICourseOverviewRepository courseOverviewRepository,
        ICourseQueryService courseQueryService)
    {
        _courseOverviewRepository = courseOverviewRepository;
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<CourseOverviewDto>> Handle(GetCourseOverviewQuery request, CancellationToken cancellationToken)
    {
        var course = await _courseQueryService.GetCourseByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var overview = await _courseOverviewRepository.GetByCourseIdAsync(request.CourseId, cancellationToken);
        if (overview is null)
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");

        return new CourseOverviewDto(
            overview.CourseId,
            course.Status,
            overview.DescriptionContent,
            overview.TargetAudienceContent,
            overview.LearningObjectivesContent,
            overview.CoverImageDocumentId,
            overview.ETag);
    }
}
