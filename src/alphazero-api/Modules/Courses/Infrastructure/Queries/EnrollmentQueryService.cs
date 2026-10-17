using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
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
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active);

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
        return await _context.Set<Enrollment>()
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
    }

    public async Task<PagedResult<EnrollmentDto>> GetCourseEnrollmentsAsync(Guid courseId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.CourseId == courseId);

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

    public async Task<List<EnrollmentDto>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Enrollment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.EnrolledOn)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status.ToString(),
                e.Progress.CompletionPercentage,
                e.EnrolledOn,
                e.TenantId))
            .ToListAsync(cancellationToken);
    }

    public async Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status.ToString(),
                e.Progress.CompletionPercentage,
                e.EnrolledOn,
                e.TenantId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The view endpoint's single enrollment lookup (#32): scoped to the CALLER's own
    /// (student × course), active only. Returns the completed bit indexes (computed
    /// client-side from the varbit bitmask) so the view can overlay them on the
    /// curriculum tree.
    /// </summary>
    public async Task<EnrollmentCourseViewDto?> GetActiveEnrollmentForCourseAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.CourseId == courseId && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.EnrolledOn)
            .FirstOrDefaultAsync(cancellationToken);

        if (enrollment is null)
        {
            return null;
        }

        var completedBitIndexes = new List<int>();
        for (var bitIndex = 0; bitIndex < enrollment.Progress.Bitmask.Length; bitIndex++)
        {
            if (enrollment.Progress.Bitmask[bitIndex])
            {
                completedBitIndexes.Add(bitIndex);
            }
        }

        return new EnrollmentCourseViewDto(
            enrollment.Id,
            enrollment.Status.ToString(),
            enrollment.Progress.CompletionPercentage,
            completedBitIndexes);
    }
}
