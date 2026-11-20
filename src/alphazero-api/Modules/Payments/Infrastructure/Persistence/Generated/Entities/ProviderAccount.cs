using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ProviderAccountDataModel
{
    public Guid Id { get; set; }

    public string ProviderName { get; set; } = null!;

    public string Environment { get; set; } = null!;

    public string ExternalAccountId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string CredentialSecretName { get; set; } = null!;

    public string WebhookSecretName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<DisputeDataModel> Disputes { get; set; } = new List<DisputeDataModel>();

    public virtual ICollection<LedgerAccountDataModel> LedgerAccounts { get; set; } = new List<LedgerAccountDataModel>();

    public virtual ICollection<LedgerEntryDataModel> LedgerEntries { get; set; } = new List<LedgerEntryDataModel>();

    public virtual ICollection<PaymentReviewCaseDataModel> PaymentReviewCases { get; set; } = new List<PaymentReviewCaseDataModel>();

    public virtual ICollection<ProductDailyPurchaseDataModel> ProductDailyPurchases { get; set; } = new List<ProductDailyPurchaseDataModel>();

    public virtual ICollection<ProviderCurrencyRouteDataModel> ProviderCurrencyRoutes { get; set; } = new List<ProviderCurrencyRouteDataModel>();

    public virtual PaymentProviderDataModel ProviderNameNavigation { get; set; } = null!;

    public virtual ICollection<ProviderTransactionDataModel> ProviderTransactions { get; set; } = new List<ProviderTransactionDataModel>();

    public virtual ICollection<PurchaseDataModel> Purchases { get; set; } = new List<PurchaseDataModel>();

    public virtual ICollection<ReconciliationRunDataModel> ReconciliationRuns { get; set; } = new List<ReconciliationRunDataModel>();

    public virtual ICollection<RefundDataModel> Refunds { get; set; } = new List<RefundDataModel>();

    public virtual ICollection<SettlementDataModel> Settlements { get; set; } = new List<SettlementDataModel>();

    public virtual ICollection<WebhookEventDataModel> WebhookEvents { get; set; } = new List<WebhookEventDataModel>();
}
