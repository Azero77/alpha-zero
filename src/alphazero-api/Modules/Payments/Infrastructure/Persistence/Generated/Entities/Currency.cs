using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class CurrencyDataModel
{
    public string Code { get; set; } = null!;

    public short Exponent { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<DisputeDataModel> Disputes { get; set; } = new List<DisputeDataModel>();

    public virtual ICollection<LedgerAccountDataModel> LedgerAccounts { get; set; } = new List<LedgerAccountDataModel>();

    public virtual ICollection<LedgerLineDataModel> LedgerLines { get; set; } = new List<LedgerLineDataModel>();

    public virtual ICollection<PaymentReviewCaseDataModel> PaymentReviewCases { get; set; } = new List<PaymentReviewCaseDataModel>();

    public virtual ICollection<PriceDataModel> Prices { get; set; } = new List<PriceDataModel>();

    public virtual ICollection<ProductDailyPurchaseDataModel> ProductDailyPurchases { get; set; } = new List<ProductDailyPurchaseDataModel>();

    public virtual ICollection<ProviderCurrencyRouteDataModel> ProviderCurrencyRouteIntegrationCurrencyNavigations { get; set; } = new List<ProviderCurrencyRouteDataModel>();

    public virtual ICollection<ProviderCurrencyRouteDataModel> ProviderCurrencyRouteSettlementCurrencyNavigations { get; set; } = new List<ProviderCurrencyRouteDataModel>();

    public virtual ICollection<ProviderTransactionDataModel> ProviderTransactions { get; set; } = new List<ProviderTransactionDataModel>();

    public virtual ICollection<PurchaseDataModel> PurchasePresentmentCurrencyNavigations { get; set; } = new List<PurchaseDataModel>();

    public virtual ICollection<PurchaseDataModel> PurchaseSettlementCurrencyNavigations { get; set; } = new List<PurchaseDataModel>();

    public virtual ICollection<RefundRequestDataModel> RefundRequests { get; set; } = new List<RefundRequestDataModel>();

    public virtual ICollection<RefundDataModel> Refunds { get; set; } = new List<RefundDataModel>();

    public virtual ICollection<SettlementDataModel> Settlements { get; set; } = new List<SettlementDataModel>();
}
