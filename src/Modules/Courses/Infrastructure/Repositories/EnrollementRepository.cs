using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

public class EnrollementRepository : BaseRepository<AppDbContext, Enrollement>, IEnrollementRepository
{
    private readonly AppDbContext _context;
    public EnrollementRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Enrollement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollements
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Enrollement>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollements
            .IgnoreQueryFilters([DbContstants.TenantFilter])
            .Where(e => e.StudentId == studentId && e.Status == EnrollementStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Enrollement>> GetStudentEnrollmentsForTenantAsync(Guid studentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Enrollements
        .IgnoreQueryFilters([DbContstants.TenantFilter])
        .Where(e => e.StudentId == studentId && e.TenantId == tenantId && e.Status == EnrollementStatus.Active)
        .ToListAsync(cancellationToken);
    }

}
