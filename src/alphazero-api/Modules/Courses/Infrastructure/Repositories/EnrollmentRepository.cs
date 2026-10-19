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

    public async Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollments
            .IgnoreQueryFilters([DbContstants.TenantFilter])
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
    }
}
