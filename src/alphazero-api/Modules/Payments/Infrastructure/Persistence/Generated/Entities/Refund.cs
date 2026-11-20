using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class RefundDataModel
{
    public Guid Id { get; set; }

    public Guid PurchaseId { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProviderAccountId { get; set; }

    public Guid? ProviderTransactionId { get; set; }

    public Guid? PaymentReviewCaseId { get; set; }

    public string Source { get; set; } = null!;

    public Guid? RequestedByUserId { get; set; }

    public string? Reason { get; set; }

    public string ReasonCode { get; set; } = null!;

    public bool RevokeAccess { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string? ExternalRefundId { get; set; }

    public decimal ProviderFeeRefunded { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual LedgerEntryDataModel? LedgerEntry { get; set; }

    public virtual PaymentReviewCaseDataModel? PaymentReviewCase { get; set; }

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual ProviderTransactionDataModel? ProviderTransaction { get; set; }

    public virtual PurchaseDataModel Purchase { get; set; } = null!;

    public virtual RefundRequestDataModel? RefundRequest { get; set; }
}
