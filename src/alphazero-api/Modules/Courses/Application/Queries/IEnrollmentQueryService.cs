using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetStudentDashboard;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Application.Queries;

/// <summary>
/// Read-side port for enrollment listings. One method per endpoint need, each with its own
/// narrow projection — no generic "load everything" queries. Derivation (completed/active
/// counts, completion percentage) stays a single-source-of-truth job of
/// <see cref="Domain.Aggregates.Enrollments.ProgressCalculation"/>, applied on rows that carry
/// only the two varbit masks plus the scalar fields the endpoint serializes.
/// </summary>
public interface IEnrollmentQueryService
{
    /// <summary>Support listing (GET /courses/enrollments): all statuses of one student, paged, tenant-scoped.</summary>
    Task<PagedResult<EnrollmentDto>> ListStudentEnrollmentsAsync(Guid studentId, int page, int perPage, CancellationToken cancellationToken = default);

    /// <summary>Analytics listing (GET /courses/{CourseId}/students): enrollments of one course, paged, tenant-scoped.</summary>
    Task<PagedResult<EnrollmentDto>> ListCourseEnrollmentsAsync(Guid courseId, int page, int perPage, CancellationToken cancellationToken = default);

    /// <summary>Dashboard (GET /courses/dashboard/{StudentId}): active enrollments across tenants, intentionally unfiltered.</summary>
    Task<List<DashboardEnrollmentRow>> ListStudentDashboardEnrollmentsAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>Enrollment detail (GET /courses/enrollments/{Id}): single enrollment, tenant-scoped.</summary>
    Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
}
