using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class PriceDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CheckoutSessionDataModel> CheckoutSessions { get; set; } = new List<CheckoutSessionDataModel>();

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual ProductDataModel Product { get; set; } = null!;

    public virtual ICollection<PurchaseDataModel> Purchases { get; set; } = new List<PurchaseDataModel>();
}
