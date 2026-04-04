using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Subjects.Queries.ListSubjects;

public record ListSubjectsQuery(int Page = 1, int PerPage = 10) : IRequest<ErrorOr<PagedResult<Subject>>>;

public sealed class ListSubjectsQueryHandler : IRequestHandler<ListSubjectsQuery, ErrorOr<PagedResult<Subject>>>
{
    private readonly ISubjectRepository _subjectRepository;

    public ListSubjectsQueryHandler(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<ErrorOr<PagedResult<Subject>>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        return await _subjectRepository.Get(
            request.Page,
            request.PerPage,
            orderBy: s => s.Name,
            ascending: true,
            token: cancellationToken);
    }
}
