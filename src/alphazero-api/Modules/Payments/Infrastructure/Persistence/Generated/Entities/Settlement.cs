using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class SettlementDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProviderAccountId { get; set; }

    public Guid PaymentReviewCaseId { get; set; }

    public Guid PayableAccountId { get; set; }

    public Guid SourceAccountId { get; set; }

    public Guid? ProviderTransactionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public Guid RecordedByUserId { get; set; }

    public Guid ApprovedByUserId { get; set; }

    public DateTime RecordedAt { get; set; }

    public string ExternalReference { get; set; } = null!;

    public string DestinationSnapshot { get; set; } = null!;

    public string EvidenceReference { get; set; } = null!;

    public string? Notes { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual LedgerEntryDataModel? LedgerEntry { get; set; }

    public virtual LedgerAccountDataModel PayableAccount { get; set; } = null!;

    public virtual PaymentReviewCaseDataModel PaymentReviewCase { get; set; } = null!;

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual ProviderTransactionDataModel? ProviderTransaction { get; set; }

    public virtual LedgerAccountDataModel SourceAccount { get; set; } = null!;
}
