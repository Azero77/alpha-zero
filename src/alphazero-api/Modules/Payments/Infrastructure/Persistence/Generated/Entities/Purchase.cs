using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class PurchaseDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid ProductId { get; set; }

    public Guid PriceId { get; set; }

    public Guid CheckoutSessionId { get; set; }

    public Guid ProviderAccountId { get; set; }

    public Guid ProviderTransactionId { get; set; }

    public string ItemTitle { get; set; } = null!;

    public decimal PresentmentAmount { get; set; }

    public string PresentmentCurrency { get; set; } = null!;

    public decimal SettlementAmount { get; set; }

    public string SettlementCurrency { get; set; } = null!;

    public decimal? FxRate { get; set; }

    public decimal ProviderFee { get; set; }

    public decimal CommissionShare { get; set; }

    public string ExternalPaymentId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime PaidAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public virtual CheckoutSessionDataModel CheckoutSession { get; set; } = null!;

    public virtual ICollection<DisputeDataModel> Disputes { get; set; } = new List<DisputeDataModel>();

    public virtual LedgerEntryDataModel? LedgerEntry { get; set; }

    public virtual ICollection<PaymentReviewCaseDataModel> PaymentReviewCases { get; set; } = new List<PaymentReviewCaseDataModel>();

    public virtual CurrencyDataModel PresentmentCurrencyNavigation { get; set; } = null!;

    public virtual PriceDataModel Price { get; set; } = null!;

    public virtual ProductDataModel Product { get; set; } = null!;

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual ProviderTransactionDataModel ProviderTransaction { get; set; } = null!;

    public virtual RefundRequestDataModel? RefundRequest { get; set; }

    public virtual ICollection<RefundDataModel> Refunds { get; set; } = new List<RefundDataModel>();

    public virtual CurrencyDataModel SettlementCurrencyNavigation { get; set; } = null!;
}
