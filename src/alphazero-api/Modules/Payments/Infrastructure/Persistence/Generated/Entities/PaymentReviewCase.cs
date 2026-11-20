using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class PaymentReviewCaseDataModel
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid ProviderAccountId { get; set; }

    public Guid? PayableAccountId { get; set; }

    public Guid? CheckoutSessionId { get; set; }

    public Guid? PurchaseId { get; set; }

    public Guid? ProviderTransactionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public Guid? SourceAccountId { get; set; }

    public string? DestinationSnapshot { get; set; }

    public string? ReasonCode { get; set; }

    public bool? RevokeAccess { get; set; }

    public string Type { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid OpenedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public string? ProviderSupportReference { get; set; }

    public string EvidenceReference { get; set; } = null!;

    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual CheckoutSessionDataModel? CheckoutSession { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual ICollection<LedgerEntryDataModel> LedgerEntries { get; set; } = new List<LedgerEntryDataModel>();

    public virtual LedgerAccountDataModel? PayableAccount { get; set; }

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual ProviderTransactionDataModel? ProviderTransaction { get; set; }

    public virtual PurchaseDataModel? Purchase { get; set; }

    public virtual ICollection<RefundDataModel> Refunds { get; set; } = new List<RefundDataModel>();

    public virtual SettlementDataModel? Settlement { get; set; }

    public virtual LedgerAccountDataModel? SourceAccount { get; set; }
}
