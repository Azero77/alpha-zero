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
        // D3 retry fresh-read: detach only what we are about to re-read — a tracker-wide
        // Clear() would evict unrelated tracked entities if a caller ever used a pooled
        // context. Keep the contract narrow because callers trust the narrow name.
        var tracked = _context.Set<Enrollment>().Local.FirstOrDefault(e => e.Id == id);
        if (tracked is not null)
        {
            var entry = _context.Entry(tracked);
            entry.State = EntityState.Detached;

            // EF detaches the root only along its current owned references; owned rows
            // replaced by the failed attempt's apply linger behind as orphaned entries,
            // and an orphaned owned entry poisons the re-materialization below (the
            // fresh instance comes back with its owned parts unbound). Evict the root's
            // owned rows so the fresh read covers exactly this aggregate — root plus its
            // own owned rows — and nothing else the tracker still holds.
            foreach (var leftover in _context.ChangeTracker.Entries())
                if (leftover.Metadata.IsOwned()
                    && leftover.Metadata.FindOwnership()?.PrincipalEntityType == entry.Metadata)
                    leftover.State = EntityState.Detached;
        }

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
