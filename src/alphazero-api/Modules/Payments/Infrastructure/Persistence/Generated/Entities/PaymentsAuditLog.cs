using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class PaymentsAuditLogDataModel
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid? TenantId { get; set; }

    public string Action { get; set; } = null!;

    public Guid? AggregateId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? Payload { get; set; }

    public DateTime OccurredAt { get; set; }
}
