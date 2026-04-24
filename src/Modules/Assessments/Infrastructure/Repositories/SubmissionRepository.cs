using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Submissions;
using AlphaZero.Modules.Assessments.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Assessments.Infrastructure.Repositories;

public class SubmissionRepository : BaseRepository<AppDbContext, AssessmentSubmission>, ISubmissionRepository
{
    public SubmissionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AssessmentSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Submissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }
}
