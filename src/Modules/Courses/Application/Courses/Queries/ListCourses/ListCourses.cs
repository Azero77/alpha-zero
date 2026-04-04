using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;

public record ListCoursesQuery(Guid? SubjectId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<Course>>>;

public sealed class ListCoursesQueryHandler : IRequestHandler<ListCoursesQuery, ErrorOr<PagedResult<Course>>>
{
    private readonly ICourseRepository _courseRepository;

    public ListCoursesQueryHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<ErrorOr<PagedResult<Course>>> Handle(ListCoursesQuery request, CancellationToken cancellationToken)
    {
        if (request.SubjectId.HasValue)
        {
            return await _courseRepository.Get(
                request.Page,
                request.PerPage,
                filter: c => c.SubjectId == request.SubjectId.Value,
                orderBy: c => c.Title,
                ascending: true,
                token: cancellationToken);
        }

        return await _courseRepository.Get(
            request.Page,
            request.PerPage,
            orderBy: c => c.Title,
            ascending: true,
            token: cancellationToken);
    }
}
