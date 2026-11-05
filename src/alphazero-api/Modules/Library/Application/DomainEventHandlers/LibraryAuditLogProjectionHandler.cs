using AlphaZero.Modules.Library.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.DomainEventHandlers;

/// <summary>
/// Single projection handler for the LibraryAuditLog append-only stream.
/// Listens to all 6 lifecycle domain events raised by the AccessCode aggregate and writes the corresponding audit row.
/// Invariant I-AUDIT: every lifecycle transition produces exactly one attributable stream row.
/// Invariant I-STATE: the stream is a projection of state-machine events.
/// </summary>
public sealed class LibraryAuditLogProjectionHandler(
    ILibraryAuditLogRepository auditLogRepository,
    ILogger<LibraryAuditLogProjectionHandler> logger) :
    INotificationHandler<AccessCodeMintedDomainEvent>,
    INotificationHandler<AccessCodeDistributedDomainEvent>,
    INotificationHandler<AccessCodeSoldDomainEvent>,
    INotificationHandler<AccessCodeRedeemedDomainEvent>,
    INotificationHandler<AccessCodeVoidedDomainEvent>,
    INotificationHandler<AccessCodeExpiredDomainEvent>
{
    public Task Handle(AccessCodeMintedDomainEvent notification, CancellationToken cancellationToken)
        => ProjectAsync(notification, cancellationToken);

    public Task Handle(AccessCodeDistributedDomainEvent notification, CancellationToken cancellationToken)
        => ProjectAsync(notification, cancellationToken);

    public Task Handle(AccessCodeSoldDomainEvent notification, CancellationToken cancellationToken)
        => ProjectAsync(notification, cancellationToken);

    public Task Handle(AccessCodeRedeemedDomainEvent notification, CancellationToken cancellationToken)
        => ProjectAsync(notification, cancellationToken);

    public Task Handle(AccessCodeVoidedDomainEvent notification, CancellationToken cancellationToken)
        => ProjectAsync(notification, cancellationToken);

    public Task Handle(AccessCodeExpiredDomainEvent notification, CancellationToken cancellationToken)
        => ProjectAsync(notification, cancellationToken);

    private async Task ProjectAsync(ILibraryLifecycleDomainEvent notification, CancellationToken cancellationToken)
    {
        var auditEntry = LibraryAuditLog.Create(
            notification.Id,
            notification.TenantId,
            notification.LibraryId,
            notification.ActorUserId,
            notification.Action,
            notification.AggregateId,
            notification.Payload,
            notification.OccurredAt);

        await auditLogRepository.AddAsync(auditEntry, cancellationToken);

        logger.LogDebug(
            "Projected LibraryAuditLog: Action={Action}, AggregateId={AggregateId}, EventId={EventId}, TenantId={TenantId}, LibraryId={LibraryId}",
            notification.Action, notification.AggregateId, notification.Id, notification.TenantId, notification.LibraryId);
    }
}
