using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollmentRepository : IRepository<Enrollment>
{
    Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The retry's fresh load evicts exactly the locally-tracked instance of this enrollment and its owned dependents (entity-scoped; not a tracker-wide reset) and returns a NEW instance re-read from the database.
    /// This module's contract methods run on its own scoped AppDbContext; other modules register their own contexts, so this eviction cannot cross module boundaries today — revisit this sentence before introducing a pooled/long-lived context.
    /// </summary>
    Task<Enrollment?> GetFreshByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
}
