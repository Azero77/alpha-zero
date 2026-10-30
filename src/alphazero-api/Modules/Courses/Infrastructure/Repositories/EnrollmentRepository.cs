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
    /// Pessimistic row-level lock (#32/D3): acquires a Postgres FOR UPDATE lock on the
    /// enrollment row so concurrent completions serialise at the database level — no
    /// application-side retry, no ChangeTracker detach/re-read surgery.
    ///
    /// One statement is enough here only because <see cref="Enrollment.Progress"/> is mapped as a
    /// complex property. When it was an owned type, EF composed this query as a self LEFT JOIN to
    /// the same table for the owned columns; the join alias was scanned with the snapshot taken
    /// before the lock wait, so the losing writer still materialised the pre-lock bitmask and
    /// dropped the winner's bit. Do not change Progress back to OwnsOne.
    ///
    /// The lock is held until the ambient transaction (see TransactionalBehavior) commits, which
    /// is what makes the read in the same statement observe the previous writer's committed row.
    /// </summary>
    public async Task<Enrollment?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Outside a transaction the lock is released as soon as the statement ends: a silent
        // no-op that reintroduces the lost update. Fail loudly instead.
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "GetByIdForUpdateAsync requires an ambient transaction; FOR UPDATE is released at statement end otherwise.");

        // EF 10 FromSql composition projects complex-type columns under their default name
        // ("Progress_Bitmask") instead of the configured HasColumnName("ProgressBitmask") —
        // normal queries and INSERTs use the configured name. Alias it here so the composed
        // projection resolves against the real column. Harmless if EF ever stops doing this.
        return await _context.Enrollments
            .FromSqlInterpolated($"""
                SELECT e.*, e."ProgressBitmask" AS "Progress_Bitmask"
                FROM "Courses"."Enrollements" AS e
                WHERE e."Id" = {id}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .IgnoreQueryFilters([DbContstants.TenantFilter])
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
    }
}
