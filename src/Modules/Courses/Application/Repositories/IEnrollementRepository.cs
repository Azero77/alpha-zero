using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollements;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollementRepository : IRepository<Enrollement>
{
    Task<Enrollement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Enrollement>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<List<Enrollement>> GetStudentEnrollmentsForTenantAsync(Guid studentId,Guid tenantId, CancellationToken cancellationToken = default);
}
