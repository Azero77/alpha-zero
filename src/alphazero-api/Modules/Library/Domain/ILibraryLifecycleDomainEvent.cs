using System.Text.Json;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Library.Domain;

/// <summary>
/// Defines the contract for all state machine lifecycle domain events that project into the LibraryAuditLog stream.
/// </summary>
public interface ILibraryLifecycleDomainEvent : IDomainEvent
{
    Guid AggregateId { get; }
    Guid TenantId { get; }
    Guid? LibraryId { get; }
    Guid? ActorUserId { get; }
    string Action { get; }
    JsonDocument Payload { get; }
    DateTime OccurredAt { get; }
}
