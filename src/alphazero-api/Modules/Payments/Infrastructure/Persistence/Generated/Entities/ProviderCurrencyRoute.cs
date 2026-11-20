using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ProviderCurrencyRouteDataModel
{
    public Guid Id { get; set; }

    public Guid ProviderAccountId { get; set; }

    public string IntegrationCurrency { get; set; } = null!;

    public string SettlementCurrency { get; set; } = null!;

    public string PresentmentMode { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public short ShortHoldDays { get; set; }

    public decimal ReserveRate { get; set; }

    public short DisputeHoldDays { get; set; }

    public bool RequiresDailyReconciliation { get; set; }

    public virtual ICollection<AcademyProviderRiskOverrideDataModel> AcademyProviderRiskOverrides { get; set; } = new List<AcademyProviderRiskOverrideDataModel>();

    public virtual ICollection<CheckoutSessionDataModel> CheckoutSessions { get; set; } = new List<CheckoutSessionDataModel>();

    public virtual CurrencyDataModel IntegrationCurrencyNavigation { get; set; } = null!;

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual CurrencyDataModel SettlementCurrencyNavigation { get; set; } = null!;
}
