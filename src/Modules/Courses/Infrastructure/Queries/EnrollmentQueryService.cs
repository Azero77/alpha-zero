using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

public class EnrollmentQueryService : IEnrollmentQueryService
{
    private readonly AppDbContext _context;

    public EnrollmentQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EnrollmentDto>> GetStudentEnrollmentsAsync(Guid studentId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Enrollement>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollementStatus.Active);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.EnrolledOn)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status.ToString(),
                e.Progress.CompletionPercentage,
                e.EnrolledOn,
                e.TenantId))
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(items, totalCount, page, perPage);
    }

    public async Task<List<EnrollmentDto>> GetStudentEnrollmentsForTenantAsync(Guid studentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Enrollement>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.TenantId == tenantId)
            .Select(e => new EnrollmentDto(
            e.Id,
            e.StudentId,
            e.CourseId,
            e.Status.ToString(),
            e.Progress.CompletionPercentage,
            e.EnrolledOn,
            e.TenantId)).ToListAsync(cancellationToken);
            ;

    }
}
