using AlphaZero.Modules.Assessments.Application.Repositories;
using AlphaZero.Modules.Assessments.Domain.Aggregates.Assessments;
using AlphaZero.Modules.Assessments.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Assessments.Infrastructure.Repositories;

public class AssessmentRepository : BaseRepository<AppDbContext, Assessment>, IAssessmentRepository
{
    public AssessmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Assessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Assessments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Assessment?> GetByIdWithCurrentVersionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Assessments
            .Include(a => a.Versions.Where(v => v.Id == a.CurrentVersionId))
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Assessment?> GetByIdWithVersionAsync(Guid id, Guid versionId, CancellationToken cancellationToken = default)
    {
        return await _context.Assessments
            .Include(a => a.Versions.Where(v => v.Id == versionId))
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
