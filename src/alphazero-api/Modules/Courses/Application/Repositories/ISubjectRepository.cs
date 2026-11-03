using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface ISubjectRepository : IRepository<Subject>
{
    Task<Subject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
