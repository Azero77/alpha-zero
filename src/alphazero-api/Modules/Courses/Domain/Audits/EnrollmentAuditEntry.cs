using AlphaZero.Shared.Infrastructure.Tenats;

namespace AlphaZero.Modules.Courses.Domain.Audits;

/// <summary>
/// Append-only lifecycle audit row (issue #34 §3.1). Generic columns so new event kinds join
/// without a schema change; replaces the <c>LastStatusChanged*</c> plumbing that used to live on
/// the Enrollment aggregate — that aggregate carries no update serves-only-read data any more.
///
/// The writer is the in-process Courses <c>DomainEventHandler</c> (MediatR notification handlers),
/// which stages rows on the SAME module DbContext the aggregate mutation touches — the UnitOfWork
/// publishes domain events BEFORE its single SaveChangesAsync, so a row here commits atomically
/// with the transition it describes. <see cref="SubjectId"/> is deliberately a generic Guid
/// reference (no FK): it names the enrollment this row is about.
/// </summary>
public class EnrollmentAuditEntry : TenantOwnedEntity
{
    /// <summary>The enrollment the row is about.</summary>
    public Guid SubjectId { get; }

    /// <summary>What happened (transition kind).</summary>
    public EnrollmentAuditEventKind EventKind { get; }

    /// <summary>The acting principal, or null for automated transitions (saga / expiry sweep).</summary>
    public Guid? ActorId { get; }

    /// <summary>When the transition happened (the domain event's stamp, not the row's creation time).</summary>
    public DateTime OccurredOn { get; }

    /// <summary>
    /// Free-form jsonb context: <c>{"From": …, "To": …, "cause": …}</c>. Presently the transition's
    /// source/target status and, where an event carries one, the human cause (suspension reason).
    /// </summary>
    public Dictionary<string, string> Metadata { get; }

    private EnrollmentAuditEntry()
        : base(Guid.NewGuid(), Guid.Empty) // EF materializer; TenantOwnedEntity exposes no empty ctor
    {
    }

    private EnrollmentAuditEntry(
        Guid id, Guid tenantId, Guid subjectId, EnrollmentAuditEventKind kind,
        Guid? actorId, DateTime occurredOn, Dictionary<string, string> metadata)
        : base(id, tenantId)
    {
        SubjectId = subjectId;
        EventKind = kind;
        ActorId = actorId;
        OccurredOn = occurredOn;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates one row. The id is generated here (the repo pins explicit ids with
    /// ValueGeneratedNever); <see cref="OccurredOn"/> is the event's stamp so re-ordered handler
    /// execution cannot skew the audit timeline.
    /// </summary>
    public static EnrollmentAuditEntry Create(
        Guid tenantId,
        Guid subjectId,
        EnrollmentAuditEventKind kind,
        Guid? actorId,
        DateTime occurredOn,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var copy = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata);
        return new EnrollmentAuditEntry(Guid.NewGuid(), tenantId, subjectId, kind, actorId, occurredOn, copy);
    }
}
