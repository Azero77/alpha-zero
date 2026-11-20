using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ProductDailyPurchaseDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProviderAccountId { get; set; }

    public Guid ProductId { get; set; }

    public DateOnly Day { get; set; }

    public string SettlementCurrency { get; set; } = null!;

    public int PurchaseCount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal NetAcademyAmount { get; set; }

    public int RefundCount { get; set; }

    public decimal RefundedGrossAmount { get; set; }

    public decimal RefundedCommissionAmount { get; set; }

    public decimal RefundedNetAcademyAmount { get; set; }

    public DateTime RefreshedAt { get; set; }

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual CurrencyDataModel SettlementCurrencyNavigation { get; set; } = null!;
}
