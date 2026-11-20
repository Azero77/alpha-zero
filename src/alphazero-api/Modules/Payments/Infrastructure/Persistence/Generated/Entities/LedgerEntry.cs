using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class LedgerEntryDataModel
{
    public Guid Id { get; set; }

    public DateTime PostedAt { get; set; }

    public DateTime? EffectiveAt { get; set; }

    public string Type { get; set; } = null!;

    public Guid? TenantId { get; set; }

    public Guid? ProviderAccountId { get; set; }

    public Guid? ProviderTransactionId { get; set; }

    public Guid? PurchaseId { get; set; }

    public Guid? RefundId { get; set; }

    public Guid? SettlementId { get; set; }

    public Guid? DisputeId { get; set; }

    public Guid? PaymentReviewCaseId { get; set; }

    public string? ExternalObjectId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string? Description { get; set; }

    public virtual DisputeDataModel? Dispute { get; set; }

    public virtual ICollection<LedgerLineDataModel> LedgerLines { get; set; } = new List<LedgerLineDataModel>();

    public virtual PaymentReviewCaseDataModel? PaymentReviewCase { get; set; }

    public virtual ProviderAccountDataModel? ProviderAccount { get; set; }

    public virtual ProviderTransactionDataModel? ProviderTransaction { get; set; }

    public virtual PurchaseDataModel? Purchase { get; set; }

    public virtual RefundDataModel? Refund { get; set; }

    public virtual SettlementDataModel? Settlement { get; set; }
}
