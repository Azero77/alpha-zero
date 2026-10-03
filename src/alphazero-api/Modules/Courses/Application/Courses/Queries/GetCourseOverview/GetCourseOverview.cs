using ErrorOr;
using MediatR;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Queries;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;

public record CourseOverviewDto(
    Guid Id,
    Guid CourseId,
    string CourseStatus,
    string DescriptionContent,
    string? TargetAudienceContent,
    string? LearningObjectivesContent,
    Guid? CoverImageDocumentId,
    string ETag);

public record GetCourseOverviewQuery(Guid CourseId) : IRequest<ErrorOr<CourseOverviewDto>>;

public class GetCourseOverviewQueryHandler : IRequestHandler<GetCourseOverviewQuery, ErrorOr<CourseOverviewDto>>
{
    private readonly ICourseOverviewRepository _courseOverviewRepository;

    public GetCourseOverviewQueryHandler(
        ICourseOverviewRepository courseOverviewRepository
        )
    {
        _courseOverviewRepository = courseOverviewRepository;
    }

    public async Task<ErrorOr<CourseOverviewDto>> Handle(GetCourseOverviewQuery request, CancellationToken cancellationToken)
    {
        var overview = await _courseOverviewRepository.GetByCourseIdAsync(request.CourseId, cancellationToken);
        if (overview is null)
            return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");

        return overview;
    }
}
