using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourse;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;
using System.Linq;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;

public record ListCoursesQuery(Guid? SubjectId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<CourseDto>>>;

public sealed class ListCoursesQueryHandler : IRequestHandler<ListCoursesQuery, ErrorOr<PagedResult<CourseDto>>>
{
    private readonly ICourseRepository _courseRepository;

    public ListCoursesQueryHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<ErrorOr<PagedResult<CourseDto>>> Handle(ListCoursesQuery request, CancellationToken cancellationToken)
    {
        var result = request.SubjectId.HasValue
            ? await _courseRepository.Get(
                request.Page,
                request.PerPage,
                filter: c => c.SubjectId == request.SubjectId.Value,
                orderBy: c => c.Title,
                ascending: true,
                token: cancellationToken)
            : await _courseRepository.Get(
                request.Page,
                request.PerPage,
                orderBy: c => c.Title,
                ascending: true,
                token: cancellationToken);

        return new PagedResult<CourseDto>(
            result.Items.Select(course => new CourseDto(
                course.Id,
                course.Title,
                course.Description,
                course.SubjectId,
                course.Status.ToString(),
                new List<SectionDto>())).ToList(), // ListCourses typically doesn't need full sections
            result.TotalCount,
            result.CurrentPage,
            result.PageSize);
    }
}
