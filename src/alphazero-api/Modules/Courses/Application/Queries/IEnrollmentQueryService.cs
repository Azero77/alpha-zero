using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetEnrollment;
using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface IEnrollmentQueryService
{
    Task<PagedResult<EnrollmentDto>> GetStudentEnrollmentsAsync(Guid studentId, int page, int perPage, CancellationToken cancellationToken = default);
    Task<List<EnrollmentDto>> GetStudentEnrollmentsForTenantAsync(Guid studentId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<EnrollmentDto>> GetCourseEnrollmentsAsync(Guid courseId, int page, int perPage, CancellationToken cancellationToken = default);
    Task<List<EnrollmentDto>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<EnrollmentDto?> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task<EnrollmentCourseViewDto?> GetActiveEnrollmentForCourseAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default);
}
