using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Queries.ListCourses;

public record CourseSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    Guid SubjectId,
    string Status);

public record ListCoursesQuery(Guid? SubjectId, int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<CourseSummaryDto>>>;

public sealed class ListCoursesQueryHandler : IRequestHandler<ListCoursesQuery, ErrorOr<PagedResult<CourseSummaryDto>>>
{
    private readonly ICourseQueryService _courseQueryService;

    public ListCoursesQueryHandler(ICourseQueryService courseQueryService)
    {
        _courseQueryService = courseQueryService;
    }

    public async Task<ErrorOr<PagedResult<CourseSummaryDto>>> Handle(ListCoursesQuery request, CancellationToken cancellationToken)
    {
        return await _courseQueryService.ListCoursesAsync(request.SubjectId, request.Page, request.PerPage, cancellationToken);
    }
}
