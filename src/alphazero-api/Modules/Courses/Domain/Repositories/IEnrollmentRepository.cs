using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments
{
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

        /// <summary>
        /// Resolves the student's enrollment in a specific course (issue #34 step 8: the
        /// PrincipalExpired lazy-mark path keys on (UserId, course ARN)). The tenant scope comes from
        /// the ambient tenant filter on Enrollments — a foreign-tenant pair resolves to null, never
        /// to another tenant's row.
        /// </summary>
        Task<Enrollment?> GetByStudentAndCourseAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default);

        Task<List<Enrollment>> GetStudentEnrollmentsAcrossTenantsAsync(Guid studentId, CancellationToken cancellationToken = default);
    }
}