using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseReadiness;

public record GetCourseReadinessQuery(Guid CourseId) : IQuery<CourseReadiness>;

public class GetCourseReadinessQueryValidator : AbstractValidator<GetCourseReadinessQuery>
{
    public GetCourseReadinessQueryValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
    }
}

public sealed class GetCourseReadinessQueryHandler : IRequestHandler<GetCourseReadinessQuery, ErrorOr<CourseReadiness>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICourseOverviewRepository _courseOverviewRepository;

    public GetCourseReadinessQueryHandler(
        ICourseRepository courseRepository,
        ICourseOverviewRepository courseOverviewRepository)
    {
        _courseRepository = courseRepository;
        _courseOverviewRepository = courseOverviewRepository;
    }

    public async Task<ErrorOr<CourseReadiness>> Handle(GetCourseReadinessQuery request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var overview = await _courseOverviewRepository.GetFirst(o => o.CourseId == request.CourseId, cancellationToken);
        var readiness = course.EvaluateReadiness(overview);
        return readiness;
    }
}
