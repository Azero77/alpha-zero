using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class WebhookEventDataModel
{
    public Guid Id { get; set; }

    public string ProviderName { get; set; } = null!;

    public Guid? ProviderAccountId { get; set; }

    public string ExternalEventId { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public string? ExternalObjectId { get; set; }

    public bool SignatureVerified { get; set; }

    public string Environment { get; set; } = null!;

    public bool? Livemode { get; set; }

    public int DeliveryCount { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public string? ProcessingOutcome { get; set; }

    public virtual ProviderAccountDataModel? ProviderAccount { get; set; }

    public virtual PaymentProviderDataModel ProviderNameNavigation { get; set; } = null!;

    public virtual ICollection<ProviderTransactionDataModel> ProviderTransactions { get; set; } = new List<ProviderTransactionDataModel>();

    public virtual WebhookEventPayloadDataModel? WebhookEventPayload { get; set; }
}
