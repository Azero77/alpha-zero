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
    /// Pessimistic row-level lock (#32/D3 replacement): acquires a Postgres FOR UPDATE lock on
    /// the enrollment row so concurrent completions serialise at the database level — no
    /// application-side retry, no ChangeTracker detach/re-read surgery.
    ///
    /// EF global query filters (tenant, soft-delete) are applied by composing LINQ on top of
    /// the <c>FromSqlInterpolated</c> subquery — Postgres evaluates the raw SQL as an inner
    /// relation and the filter predicates are added by EF in the outer WHERE clause.
    /// </summary>
    public async Task<Enrollment?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .FromSqlInterpolated($"""SELECT * FROM "Courses"."Enrollements" WHERE "Id" = {id} FOR UPDATE""")
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
