using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentDashboard;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Database;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Linq;

namespace AlphaZero.Modules.Courses.Infrastructure.Queries;

/// <summary>
/// Read side of enrollments. One query per endpoint, each a single round trip (the paged ones do
/// count + page: two). Every row shape is built by the one shared projection (<see cref="ProjectRows"/>):
/// the small correlated subqueries fetch only the course's mask bytes and the latest audit entry
/// (story #9, rework §3.1: last-transition who/when lives in <c>EnrollmentAuditEntries</c>, not on
/// the aggregate).
///
/// The subqueries ignore just the named filters they must: SoftDelete so an enrollment whose course
/// was soft-deleted keeps its exact derived counts (mask bytes have no tenant surface — they
/// cannot widen what rows exist). For the dashboard, which spans tenants by design, the subquery
/// additionally bypasses Tenant, preserving real numbers on every row.
///
/// The one filter relax that is NOT confined to a subquery is the dashboard's own enrollment set:
/// it drops its filters wholesale (plain <c>IgnoreQueryFilters</c>), exactly as the pre-refactor
/// <c>GetStudentEnrollmentsAcrossTenantsAsync</c> did. That behavior predates this refactor and is
/// not spec-justified — it also admits soft-deleted-but-Active enrollments, with the handler's
/// tenant grouping as the only defense. Kept here for behavior parity, but it needs an ADR or a
/// checklist amendment before anything else copies this shape.
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

        var rows = await ProjectRows(
                query.OrderByDescending(e => e.EnrolledOn).ThenBy(e => e.Id),
                includeLastTransition: true,
                courseMaskBypass: SoftDeleteOnly)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(rows.Select(Map).ToList(), totalCount, page, perPage);
    }

    public async Task<List<EnrollmentDto>> GetStudentEnrollmentsForTenantAsync(Guid studentId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Tenant-scoped support listing (issue #34 roster read path). Explicit tenant predicate —
        // not a filter relax — so the enrollment set keeps its tenant filter and the course-mask
        // subquery ignores only SoftDelete; derived counts stay exact even when the course is
        // soft-deleted.
        var rows = await ProjectRows(
                _context.Set<Enrollment>()
                    .AsNoTracking()
                    .Where(e => e.StudentId == studentId && e.TenantId == tenantId)
                    .OrderByDescending(e => e.EnrolledOn)
                    .ThenBy(e => e.Id),
                includeLastTransition: true,
                courseMaskBypass: SoftDeleteOnly)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<PagedResult<EnrollmentDto>> ListCourseEnrollmentsAsync(Guid courseId, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Enrollment>()
            .AsNoTracking()
            .Where(e => e.CourseId == courseId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await ProjectRows(
                query.OrderByDescending(e => e.EnrolledOn).ThenBy(e => e.Id),
                includeLastTransition: true,
                courseMaskBypass: SoftDeleteOnly)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>(rows.Select(Map).ToList(), totalCount, page, perPage);
    }

    public async Task<List<DashboardEnrollmentRow>> ListStudentDashboardEnrollmentsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        // The dashboard intentionally spans tenants (the handler groups by TenantId), so the
        // enrollment set drops its filters exactly as it did before this refactor — and the
        // course subquery does the same, preserving real derived numbers on every row. See the
        // class doc for why this is the one incomplete story on the file.
        var rows = await ProjectRows(
                _context.Set<Enrollment>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active)
                    .OrderByDescending(e => e.EnrolledOn)
                    .ThenBy(e => e.Id),
                includeLastTransition: false,
                courseMaskBypass: AcrossTenantsMaskReads)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDashboardRow).ToList();
    }

    public async Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var row = await ProjectRows(
                _context.Set<Enrollment>()
                    .AsNoTracking()
                    .Where(e => e.Id == enrollmentId),
                includeLastTransition: true,
                courseMaskBypass: SoftDeleteOnly)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : Map(row);
    }

    /// <summary>
    /// The view endpoint's single enrollment lookup (#32): scoped to the CALLER's own
    /// (student × course). Any status is returned — status is part of the overlay contract —
    /// while completed indexes and percentage are derived from
    /// (Progress.Bitmask, Course.ActiveItemsBitmask) through <see cref="ProgressCalculation"/>,
    /// so deleted curriculum positions never count as completed or active.
    /// </summary>
    public async Task<EnrollmentCourseViewDto?> GetEnrollmentForCourseAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default)
    {
        // ThenBy(Id) makes the newest-enrollment pick deterministic on identical EnrolledOn
        // timestamps, matching every other listing in this file.
        var row = await ProjectRows(
                _context.Set<Enrollment>()
                    .AsNoTracking()
                    .Where(e => e.StudentId == studentId && e.CourseId == courseId)
                    .OrderByDescending(e => e.EnrolledOn)
                    .ThenBy(e => e.Id),
                includeLastTransition: false,
                courseMaskBypass: SoftDeleteOnly)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var activeMask = EmptyIfNull(row.ActiveItemsBitmask);
        var sharedLength = Math.Min(row.ProgressBitmask.Length, activeMask.Length);
        var completedBitIndexes = new List<int>();
        for (var bitIndex = 0; bitIndex < sharedLength; bitIndex++)
        {
            if (row.ProgressBitmask[bitIndex] && activeMask[bitIndex])
            {
                completedBitIndexes.Add(bitIndex);
            }
        }

        return new EnrollmentCourseViewDto(
            row.Id,
            row.Status.ToString(),
            ProgressCalculation.CompletionPercentage(row.ProgressBitmask, activeMask),
            completedBitIndexes);
    }

    /// <summary>
    /// The shared row projection every endpoint consumes: scalar fields plus the two varbit masks
    /// the derived counts need plus the latest audit stamp — no full aggregate row. The course mask
    /// is null only when the course row itself is unreadable (never for a live course); the audit
    /// stamp is null when the enrollment never left Active.
    ///
    /// The audit fetch is a correlated (SubjectId, OccurredOn DESC, Id ASC) seek — descending on
    /// time, ascending on Id as the deterministic tie-break — over <c>EnrollmentAuditEntries</c>,
    /// skipping only on the two read paths whose DTOs don't expose a last stamp.
    /// </summary>
    private IQueryable<EnrollmentRow> ProjectRows(IQueryable<Enrollment> source, bool includeLastTransition, string[]? courseMaskBypass)
    {
        // The Courses set (and its named-filter bypass) is built OUTSIDE the expression tree so
        // the captured queryable references translate as a plain correlated subquery.
        var courses = courseMaskBypass is null
            ? _context.Courses
            : _context.Courses.IgnoreQueryFilters(courseMaskBypass);

        if (includeLastTransition)
        {
            return source.Select(e => new EnrollmentRow(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.Status,
                e.EnrolledOn,
                e.TenantId,
                _context.EnrollmentAuditEntries
                    .Where(a => a.SubjectId == e.Id)
                    .OrderByDescending(a => a.OccurredOn)
                    .ThenBy(a => a.Id)
                    .Select(a => new AuditStampEntry(a.ActorId, a.OccurredOn))
                    .FirstOrDefault(),
                e.Progress.Bitmask,
                courses
                    .Where(c => c.Id == e.CourseId)
                    .Select(c => c.ActiveItemsBitmask)
                    .FirstOrDefault()));
        }

        return source.Select(e => new EnrollmentRow(
            e.Id,
            e.StudentId,
            e.CourseId,
            e.Status,
            e.EnrolledOn,
            e.TenantId,
            null,
            e.Progress.Bitmask,
            courses
                .Where(c => c.Id == e.CourseId)
                .Select(c => c.ActiveItemsBitmask)
                .FirstOrDefault()));
    }

    private static EnrollmentDto Map(EnrollmentRow row)
    {
        var progress = DeriveProgress(row.ProgressBitmask, row.ActiveItemsBitmask);

        // Story #9 (rework §3.1): the DTO field names are the external contract (kept), but the
        // SOURCE is the enrollment audit trail — the aggregate no longer stamps anything.
        return new EnrollmentDto(
            row.Id,
            row.StudentId,
            row.CourseId,
            row.Status.ToString(),
            progress.CompletedActiveCount,
            progress.ActiveCount,
            progress.CompletionPercentage,
            row.EnrolledOn,
            row.TenantId,
            row.LastTransition?.OccurredOn,
            row.LastTransition?.ActorId);
    }

    private static DashboardEnrollmentRow MapToDashboardRow(EnrollmentRow row)
    {
        var progress = DeriveProgress(row.ProgressBitmask, row.ActiveItemsBitmask);

        return new DashboardEnrollmentRow(
            row.Id,
            row.CourseId,
            row.Status.ToString(),
            progress.CompletedActiveCount,
            progress.ActiveCount,
            progress.CompletionPercentage,
            row.EnrolledOn,
            row.TenantId);
    }

    /// <summary>
    /// The derived-progress trio every enrollment read exposes: intersect the learner's progress
    /// bitmask with the course's LIVE-items bitmask so deleted curriculum positions never count as
    /// completed or active. The empty-mask fallback covers the only null case (course row
    /// unreadable). One place computes it so the mappings cannot drift.
    /// </summary>
    private static ProgressCounts DeriveProgress(BitArray progressBitmask, BitArray? activeItemsBitmask)
    {
        var activeMask = EmptyIfNull(activeItemsBitmask);

        return new ProgressCounts(
            ProgressCalculation.CompletedActiveItems(progressBitmask, activeMask),
            ProgressCalculation.ActiveItems(activeMask),
            ProgressCalculation.CompletionPercentage(progressBitmask, activeMask));
    }

    private static BitArray EmptyIfNull(BitArray? mask) => mask ?? new BitArray(0);

    /// <summary>What a listing row carries: the scalar fields the endpoint shows plus the two varbit masks the derived counts need and the latest audit stamp — no full aggregate row. The course mask is null only when the course row itself is unreadable (never for a live course); the audit stamp is null when the enrollment never left Active.</summary>
    private sealed record EnrollmentRow(
        Guid Id,
        Guid StudentId,
        Guid CourseId,
        EnrollmentStatus Status,
        DateTime EnrolledOn,
        Guid TenantId,
        AuditStampEntry? LastTransition,
        BitArray ProgressBitmask,
        BitArray? ActiveItemsBitmask);

    /// <summary>The latest audit entry's who/when (story #9); kind/subtable stays server-side.</summary>
    private sealed record AuditStampEntry(Guid? ActorId, DateTime OccurredOn);

    /// <summary>Derived curriculum counts shared by every enrollment read path.</summary>
    private sealed record ProgressCounts(int CompletedActiveCount, int ActiveCount, double CompletionPercentage);
}
