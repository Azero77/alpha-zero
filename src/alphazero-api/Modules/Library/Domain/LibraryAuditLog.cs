using System.Text.Json;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Represents an immutable, append-only audit stream entry for AccessCode lifecycle transitions.
/// Invariants:
/// - I-AUDIT: every lifecycle transition produces exactly one attributable stream row; no UPDATE/DELETE path exists.
/// - I-STATE: the stream is a projection of state-machine events.
/// - I-TENANT: every row is tenant-scoped.
/// </summary>
public class LibraryAuditLog : Entity, IDomainTenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid? LibraryId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = default!;
    public Guid AggregateId { get; private set; }
    public Guid EventId { get; private set; }
    public JsonDocument Payload { get; private set; } = default!;
    public DateTime OccurredAt { get; private set; }

    private LibraryAuditLog() { }

    private LibraryAuditLog(
        Guid id,
        Guid eventId,
        Guid tenantId,
        Guid? libraryId,
        Guid? actorUserId,
        string action,
        Guid aggregateId,
        JsonDocument payload,
        DateTime occurredAt) : base(id)
    {
        EventId = eventId;
        TenantId = tenantId;
        LibraryId = libraryId;
        ActorUserId = actorUserId;
        Action = action;
        AggregateId = aggregateId;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public static LibraryAuditLog Create(
        Guid eventId,
        Guid tenantId,
        Guid? libraryId,
        Guid? actorUserId,
        string action,
        Guid aggregateId,
        JsonDocument payload,
        DateTime? occurredAt = null)
    {
        return new LibraryAuditLog(
            Guid.NewGuid(),
            eventId,
            tenantId,
            libraryId,
            actorUserId,
            action,
            aggregateId,
            payload,
            occurredAt ?? DateTime.UtcNow);
    }
}
