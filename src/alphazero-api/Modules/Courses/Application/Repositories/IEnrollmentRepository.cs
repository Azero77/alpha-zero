using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollmentRepository : IRepository<Enrollment>
{
    Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the enrollment row with a Postgres <c>SELECT … FOR UPDATE</c> row-level exclusive
    /// lock. The lock is held until the ambient transaction commits or rolls back, serialising
    /// concurrent writers on the same enrollment without application-level retry logic.
    ///
    /// Use this for any write path that mutates the enrollment's progress bitmask or completion
    /// state — it eliminates optimistic-concurrency retries and the ChangeTracker surgery they
    /// required (#32 / D3 → pessimistic locking replacement).
    /// </summary>
    Task<Enrollment?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
}
