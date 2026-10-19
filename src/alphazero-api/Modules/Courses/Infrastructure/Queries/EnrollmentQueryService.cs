using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
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
        // All statuses on purpose: this is the support listing (status is an output field; a
        // deactivated enrollment must be visible to support agents). Tenant scoping comes from
        // the global query filter.
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId);

        var totalCount = await query.CountAsync(cancellationToken);

        var enrollments = await query
            .OrderByDescending(e => e.EnrolledOn)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(await MapWithActiveMasksAsync(enrollments, cancellationToken), totalCount, page, perPage);
    }

    public async Task<List<EnrollmentDto>> GetStudentEnrollmentsForTenantAsync(Guid studentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var enrollments = await _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.TenantId == tenantId)
            .OrderByDescending(e => e.EnrolledOn)
            .ToListAsync(cancellationToken);

        return await MapWithActiveMasksAsync(enrollments, cancellationToken);
    }

    public async Task<PagedResult<EnrollmentDto>> GetCourseEnrollmentsAsync(Guid courseId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.CourseId == courseId);

        var totalCount = await query.CountAsync(cancellationToken);

        var enrollments = await query
            .OrderByDescending(e => e.EnrolledOn)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(await MapWithActiveMasksAsync(enrollments, cancellationToken), totalCount, page, perPage);
    }

    public async Task<List<EnrollmentDto>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var enrollments = await _context.Set<Enrollment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);

        return await MapWithActiveMasksAsync(enrollments, cancellationToken);
    }

    public async Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _context.Set<Enrollment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (enrollment is null)
        {
            return null;
        }

        var masks = await LoadActiveMasksAsync([enrollment.CourseId], cancellationToken);
        var mask = masks.GetValueOrDefault(enrollment.CourseId, new System.Collections.BitArray(0));

        return Map(enrollment, mask);
    }

    /// <summary>
    /// The enrollment set must never be joined against <see cref="CoursesForMaskReads"/>:
    /// <see cref="IQueryable{T}.IgnoreQueryFilters"/> is query-wide in EF Core, so a single call
    /// would also strip the tenant filter off Enrollments and leak cross-tenant rows, while the
    /// inner join would silently drop enrollments whose course row no longer exists. Instead the
    /// course masks for the already-fetched enrollments are loaded in a second, standalone query
    /// and mapping happens in memory.
    /// </summary>
    private async Task<List<EnrollmentDto>> MapWithActiveMasksAsync(List<Enrollment> enrollments, CancellationToken cancellationToken)
    {
        if (enrollments.Count == 0)
        {
            return [];
        }

        var masks = await LoadActiveMasksAsync(enrollments.Select(e => e.CourseId).Distinct().ToList(), cancellationToken);

        return enrollments
            .Select(enrollment => Map(enrollment, masks.GetValueOrDefault(enrollment.CourseId, new System.Collections.BitArray(0))))
            .ToList();
    }

    /// <summary>
    /// Course projection used only to obtain the ActiveItemsBitmask that progress percentages are
    /// derived from. It intentionally ignores the global course filters — a soft-deleted (or even
    /// fully absent) course must not make a historical enrollment disappear from
    /// support/dashboard listings (the same visibility the module had before counts were derived
    /// from the course mask). No curriculum data is loaded and no mutation happens, so tenant
    /// isolation for course WRITES is unaffected (repos + global filters). Because the query
    /// touches only Courses, ignoring its filters here is safe.
    /// </summary>
    private async Task<Dictionary<Guid, System.Collections.BitArray>> LoadActiveMasksAsync(List<Guid> courseIds, CancellationToken cancellationToken)
    {
        if (courseIds.Count == 0)
        {
            return [];
        }

        return await CoursesForMaskReads()
            .Where(c => courseIds.Contains(c.Id))
            .Select(c => new { c.Id, c.ActiveItemsBitmask })
            .ToDictionaryAsync(c => c.Id, c => c.ActiveItemsBitmask, cancellationToken);
    }

    private IQueryable<Course> CoursesForMaskReads() => _context.Courses
        .AsNoTracking()
        .IgnoreQueryFilters();

    private static EnrollmentDto Map(Enrollment enrollment, System.Collections.BitArray activeItemsBitmask)
    {
        // ProgressCalculation treats missing bits of a shorter enrollment mask as incomplete.
        var completedActiveItems = ProgressCalculation.CompletedActiveItems(enrollment.Progress.Bitmask, activeItemsBitmask);
        var activeItems = ProgressCalculation.ActiveItems(activeItemsBitmask);

        return new EnrollmentDto(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.Status.ToString(),
            completedActiveItems,
            activeItems,
            ProgressCalculation.CompletionPercentage(enrollment.Progress.Bitmask, activeItemsBitmask),
            enrollment.EnrolledOn,
            enrollment.TenantId);
    }
}
