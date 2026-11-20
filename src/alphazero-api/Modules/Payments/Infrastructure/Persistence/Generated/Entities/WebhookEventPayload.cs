using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class WebhookEventPayloadDataModel
{
    public Guid EventId { get; set; }

    public string? Payload { get; set; }

    public string? ArchiveObjectKey { get; set; }

    public string? ContentHash { get; set; }

    public string ArchiveState { get; set; } = null!;

    public DateTime? ArchivedAt { get; set; }

    public DateTime? RedactedAt { get; set; }

    public DateTime? LastArchiveAttemptAt { get; set; }

    public virtual WebhookEventDataModel Event { get; set; } = null!;
}
