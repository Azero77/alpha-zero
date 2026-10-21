using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class EnrollmentRepository : BaseRepository<AppDbContext, Enrollment>, IEnrollmentRepository
{
    public EnrollmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// The optimistic-concurrency retry's fresh load (#32/D3): a writer that lost a
    /// concurrency race must re-read the database row (a concurrent writer may have
    /// just committed a different progress bitmask and/or CompletedOn) — the scoped
    /// context's cached instances would replay the stale state that just conflicted.
    /// </summary>
    public async Task<Enrollment?> GetFreshByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .IgnoreQueryFilters([DbContstants.TenantFilter])
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
    }
}
