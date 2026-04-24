using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Assessments.Application.Repositories;

public interface IAssessmentRepository : IRepository<Assessment>
{
    Task<Assessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Assessment?> GetByIdWithCurrentVersionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Assessment?> GetByIdWithVersionAsync(Guid id, Guid versionId, CancellationToken cancellationToken = default);
}
