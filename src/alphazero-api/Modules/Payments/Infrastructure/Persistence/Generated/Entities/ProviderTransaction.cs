using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ProviderTransactionDataModel
{
    public Guid Id { get; set; }

    public Guid ProviderAccountId { get; set; }

    public Guid? WebhookEventId { get; set; }

    public string ExternalTransactionId { get; set; } = null!;

    public string? ExternalPaymentId { get; set; }

    public string? ExternalObjectId { get; set; }

    public string Type { get; set; } = null!;

    public string Direction { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public decimal FeeAmount { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual ICollection<LedgerEntryDataModel> LedgerEntries { get; set; } = new List<LedgerEntryDataModel>();

    public virtual PaymentReviewCaseDataModel? PaymentReviewCase { get; set; }

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual PurchaseDataModel? Purchase { get; set; }

    public virtual ICollection<RefundDataModel> Refunds { get; set; } = new List<RefundDataModel>();

    public virtual ICollection<SettlementDataModel> Settlements { get; set; } = new List<SettlementDataModel>();

    public virtual WebhookEventDataModel? WebhookEvent { get; set; }
}
