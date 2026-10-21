using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollmentRepository : IRepository<Enrollment>
{
    Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads an enrollment discarding any in-memory tracked state. Used by the completion
    /// write path's optimistic-concurrency retry (#32/D3): after a DbUpdateConcurrencyException
    /// the scoped context must read the database's authoritative row, including a concurrent
    /// writer's just-committed values.
    /// </summary>
    Task<Enrollment?> GetFreshByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
}
