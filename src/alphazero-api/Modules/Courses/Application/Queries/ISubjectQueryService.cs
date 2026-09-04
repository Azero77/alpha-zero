using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface ISubjectQueryService
{
    Task<PagedResult<SubjectDto>> ListSubjectsAsync(int page, int perPage, CancellationToken cancellationToken = default);
}
