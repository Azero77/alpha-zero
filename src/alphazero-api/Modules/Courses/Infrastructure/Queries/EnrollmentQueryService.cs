using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentDashboard;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Linq;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

/// <summary>
/// Read side of enrollments. One query per endpoint, each a single round trip: the enrollment
/// set stays untouched by any filter bypass (the tenant-driven filter on Enrollments is _the_
/// tenant boundary and is never relaxed here), while a small correlated subquery fetches only
/// the course's mask bytes.
///
/// The subquery ignores just the named filters it must: SoftDelete so an enrollment whose course
/// was soft-deleted keeps its exact derived counts (mask bytes have no tenant surface — they
/// cannot widen what rows exist). For the dashboard, which spans tenants by design, the subquery
/// additionally bypasses Tenant, preserving real numbers on every row.
///
/// Why a subquery and not a join: <c>IgnoreQueryFilters</c> is query-wide across entity types, so
/// joining enrollments with an unfiltered Courses set would strip the tenant filter off Enrollments
/// (cross-tenant leak) and the inner join would silently drop enrollments whose course no longer
/// exists. A correlated subquery over Courses keeps both risks on the course side only.
/// </summary>
public class EnrollmentQueryService : IEnrollmentQueryService
{
    private readonly AppDbContext _context;

    // Named-filter bypass sets hoisted out of the LINQ expression trees (collection expressions
    // are illegal inside trees; a static array reference translates fine).
    private static readonly string[] SoftDeleteOnly = [DbContstants.SoftDeleteFilter];
    private static readonly string[] AcrossTenantsMaskReads = [DbContstants.TenantFilter, DbContstants.SoftDeleteFilter];

    public EnrollmentQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EnrollmentDto>> ListStudentEnrollmentsAsync(Guid studentId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        // All statuses on purpose: this is the support listing (status is an output field; a
        // deactivated enrollment must be visible to support agents). Tenant scoping comes from
        // the enrollment query filter.
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(e => e.EnrolledOn)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status,
                e.EnrolledOn,
                e.TenantId,
                e.Progress.Bitmask,
                _context.Courses.IgnoreQueryFilters(SoftDeleteOnly)
                    .Where(c => c.Id == e.CourseId)
                    .Select(c => c.ActiveItemsBitmask)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(rows.Select(Map).ToList(), totalCount, page, perPage);
    }

    public async Task<PagedResult<EnrollmentDto>> ListCourseEnrollmentsAsync(Guid courseId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.CourseId == courseId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(e => e.EnrolledOn)
            .ThenBy(e => e.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status,
                e.EnrolledOn,
                e.TenantId,
                e.Progress.Bitmask,
                _context.Courses.IgnoreQueryFilters(SoftDeleteOnly)
                    .Where(c => c.Id == e.CourseId)
                    .Select(c => c.ActiveItemsBitmask)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(rows.Select(Map).ToList(), totalCount, page, perPage);
    }

    public async Task<List<DashboardEnrollmentRow>> ListStudentDashboardEnrollmentsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        // The dashboard intentionally spans tenants (the handler groups by TenantId), so the
        // enrollment set drops its filters exactly as it did before this refactor — and the
        // course subquery does the same, preserving real derived numbers on every row.
        var rows = await _context.Set<Enrollment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.EnrolledOn)
            .ThenBy(e => e.Id)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status,
                e.EnrolledOn,
                e.TenantId,
                e.Progress.Bitmask,
                _context.Courses.IgnoreQueryFilters(AcrossTenantsMaskReads)
                    .Where(c => c.Id == e.CourseId)
                    .Select(c => c.ActiveItemsBitmask)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDashboardRow).ToList();
    }

    public async Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var row = await _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status,
                e.EnrolledOn,
                e.TenantId,
                e.Progress.Bitmask,
                _context.Courses.IgnoreQueryFilters(SoftDeleteOnly)
                    .Where(c => c.Id == e.CourseId)
                    .Select(c => c.ActiveItemsBitmask)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : Map(row);
    }

    private static EnrollmentDto Map(EnrollmentRow row)
    {
        var courseMask = row.ActiveItemsBitmask ?? new BitArray(0);

        return new EnrollmentDto(
            row.Id,
            row.StudentId,
            row.CourseId,
            row.Status.ToString(),
            ProgressCalculation.CompletedActiveItems(row.ProgressBitmask, courseMask),
            ProgressCalculation.ActiveItems(courseMask),
            ProgressCalculation.CompletionPercentage(row.ProgressBitmask, courseMask),
            row.EnrolledOn,
            row.TenantId);
    }

    private static DashboardEnrollmentRow MapToDashboardRow(EnrollmentRow row)
    {
        var courseMask = row.ActiveItemsBitmask ?? new BitArray(0);

        return new DashboardEnrollmentRow(
            row.Id,
            row.CourseId,
            row.Status.ToString(),
            ProgressCalculation.CompletedActiveItems(row.ProgressBitmask, courseMask),
            ProgressCalculation.ActiveItems(courseMask),
            ProgressCalculation.CompletionPercentage(row.ProgressBitmask, courseMask),
            row.EnrolledOn,
            row.TenantId);
    }

    /// <summary>What a listing row carries: the scalar fields the endpoint shows plus the two varbit masks the derived counts need — no full aggregate row. The course mask is null only when the course row itself is unreadable (never for a live course).</summary>
    private sealed record EnrollmentRow(
        Guid Id,
        Guid StudentId,
        Guid CourseId,
        EnrollmentStatus Status,
        DateTime EnrolledOn,
        Guid TenantId,
        BitArray ProgressBitmask,
        BitArray? ActiveItemsBitmask);
}
