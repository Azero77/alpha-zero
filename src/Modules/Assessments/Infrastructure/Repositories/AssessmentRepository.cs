using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Infrastructure.Persistence;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Assessments.Infrastructure.Repositories;

public class AssessmentRepository : BaseRepository<AssessmentsDbContext, Assessment>, IAssessmentRepository
{
    public AssessmentRepository(AssessmentsDbContext context) : base(context)
    {
    }

    public async Task<Assessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Assessments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
