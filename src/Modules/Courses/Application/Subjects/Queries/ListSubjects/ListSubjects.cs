using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;
using System.Linq;

namespace AlphaZero.Modules.Courses.Application.Subjects.Queries.ListSubjects;

public record ListSubjectsQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<SubjectDto>>>;

public sealed class ListSubjectsQueryHandler : IRequestHandler<ListSubjectsQuery, ErrorOr<PagedResult<SubjectDto>>>
{
    private readonly ISubjectRepository _subjectRepository;

    public ListSubjectsQueryHandler(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<ErrorOr<PagedResult<SubjectDto>>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        var result = await _subjectRepository.Get(
            request.Page,
            request.PerPage,
            orderBy: s => s.Name,
            ascending: true,
            token: cancellationToken);

        return new PagedResult<SubjectDto>(
            result.Items.Select(s => new SubjectDto(s.Id, s.Name, s.Description)).ToList(),
            result.TotalCount,
            result.CurrentPage,
            result.PageSize);
    }
}
