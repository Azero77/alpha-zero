using AlphaZero.Modules.Courses.Domain.Audits;

namespace AlphaZero.Modules.Courses.Application.Repositories;

public interface IEnrollmentAuditRepository
{
    /// <summary>
    /// Stages one audit row on the module's own DbContext. The implementation deliberately does
    /// NOT save: the row is persisted by the ambient UnitOfWork.Save pipeline, which publishes
    /// domain events BEFORE its single SaveChangesAsync — so staged rows commit atomically with
    /// the aggregate mutation they describe (issue #34 §3.1; in-process, not via the bus).
    /// </summary>
    Task AddAsync(EnrollmentAuditEntry entry, CancellationToken cancellationToken = default);
}
