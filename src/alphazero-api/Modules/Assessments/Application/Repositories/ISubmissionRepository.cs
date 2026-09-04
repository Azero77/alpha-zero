using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Assessments.Application.Repositories;

public interface ISubmissionRepository : IRepository<AssessmentSubmission>
{
    Task<AssessmentSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
