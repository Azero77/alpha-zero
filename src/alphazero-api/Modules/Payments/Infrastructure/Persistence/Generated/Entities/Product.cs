using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ProductDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string ItemType { get; set; } = null!;

    public Guid ItemId { get; set; }

    public Guid? ParentContainerId { get; set; }

    public string TargetResourceArn { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string SourceAvailability { get; set; } = null!;

    public long SourceVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CheckoutSessionDataModel> CheckoutSessions { get; set; } = new List<CheckoutSessionDataModel>();

    public virtual ICollection<PriceDataModel> Prices { get; set; } = new List<PriceDataModel>();

    public virtual ICollection<PurchaseDataModel> Purchases { get; set; } = new List<PurchaseDataModel>();
}
