using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class AcademyProviderRiskOverrideDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProviderCurrencyRouteId { get; set; }

    public short? ShortHoldDays { get; set; }

    public decimal? ReserveRate { get; set; }

    public short? DisputeHoldDays { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ProviderCurrencyRouteDataModel ProviderCurrencyRoute { get; set; } = null!;
}
