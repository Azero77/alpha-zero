using AlphaZero.Modules.Courses.Application.Enrollements.Queries.GetEnrollement;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Courses.Application.Queries;

public interface IEnrollmentQueryService
{
    Task<PagedResult<EnrollmentDto>> GetStudentEnrollmentsAsync(Guid studentId, int page, int perPage, CancellationToken cancellationToken = default);
    Task<List<EnrollmentDto>> GetStudentEnrollmentsForTenantAsync(Guid studentId, Guid tenantId, CancellationToken cancellationToken = default);
}
