using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using AlphaZero.Modules.Assessments.Infrastructure.Persistence;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Assessments.Infrastructure.Repositories;

public class SubmissionRepository : BaseRepository<AssessmentsDbContext, AssessmentSubmission>, ISubmissionRepository
{
    public SubmissionRepository(AssessmentsDbContext context) : base(context)
    {
    }

    public async Task<AssessmentSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Submissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }
}
