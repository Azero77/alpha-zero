using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Analytics.Queries.ListStudentProgress;

public record ListStudentProgressQuery(Guid CourseId, int Page, int PerPage) : IRequest<ErrorOr<PagedResult<EnrollmentDto>>>;

public class ListStudentProgressQueryHandler : IRequestHandler<ListStudentProgressQuery, ErrorOr<PagedResult<EnrollmentDto>>>
{
    private readonly IEnrollmentQueryService _queryService;

    public ListStudentProgressQueryHandler(IEnrollmentQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<ErrorOr<PagedResult<EnrollmentDto>>> Handle(ListStudentProgressQuery request, CancellationToken cancellationToken)
    {
        var result = await _queryService.GetCourseEnrollmentsAsync(request.CourseId, request.Page, request.PerPage, cancellationToken);
        return result;
    }
}
