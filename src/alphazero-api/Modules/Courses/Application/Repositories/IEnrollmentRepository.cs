using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollmentRepository : IRepository<Enrollment>
{
    Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<Enrollment>> GetStudentEnrollmentsForTenantAsync(Guid studentId,Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<Enrollment>> GetCourseEnrollmentsAsync(Guid courseId, CancellationToken cancellationToken = default);
}
