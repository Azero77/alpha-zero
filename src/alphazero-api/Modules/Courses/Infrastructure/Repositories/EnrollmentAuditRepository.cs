using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Audits;
using AlphaZero.Modules.Courses.Infrastructure.Persistance;

namespace AlphaZero.Modules.Courses.Infrastructure.Repositories;

/// <summary>
/// Audit-row writer (issue #34 §3.1). Stages rows on the shared scoped <see cref="AppDbContext"/>
/// — the same context and hence the same SaveChanges the aggregate mutation commits through —
/// which is what makes an audit row atomic with the transition it describes.
/// </summary>
public class EnrollmentAuditRepository(AppDbContext context) : IEnrollmentAuditRepository
{
    public Task AddAsync(EnrollmentAuditEntry entry, CancellationToken cancellationToken = default)
        => context.EnrollmentAuditEntries.AddAsync(entry, cancellationToken).AsTask();
}
