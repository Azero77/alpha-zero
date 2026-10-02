using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Subjects.Queries.ListSubjects;

public record ListSubjectsQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<SubjectDto>>>;

public sealed class ListSubjectsQueryHandler : IRequestHandler<ListSubjectsQuery, ErrorOr<PagedResult<SubjectDto>>>
{
    private readonly ISubjectQueryService _subjectQueryService;

    public ListSubjectsQueryHandler(ISubjectQueryService subjectQueryService)
    {
        _subjectQueryService = subjectQueryService;
    }

    public async Task<ErrorOr<PagedResult<SubjectDto>>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        return await _subjectQueryService.ListSubjectsAsync(request.Page, request.PerPage, cancellationToken);
    }
}
