using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class CheckoutSessionDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid ProductId { get; set; }

    public Guid PriceId { get; set; }

    public Guid ProviderCurrencyRouteId { get; set; }

    public string? ExternalCheckoutId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public virtual ICollection<PaymentReviewCaseDataModel> PaymentReviewCases { get; set; } = new List<PaymentReviewCaseDataModel>();

    public virtual PriceDataModel Price { get; set; } = null!;

    public virtual ProductDataModel Product { get; set; } = null!;

    public virtual ProviderCurrencyRouteDataModel ProviderCurrencyRoute { get; set; } = null!;

    public virtual PurchaseDataModel? Purchase { get; set; }
}
