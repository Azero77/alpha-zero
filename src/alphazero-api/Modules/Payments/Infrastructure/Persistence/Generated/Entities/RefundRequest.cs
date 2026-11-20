using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class RefundRequestDataModel
{
    public Guid Id { get; set; }

    public Guid PurchaseId { get; set; }

    public Guid TenantId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public decimal? ApprovedAmount { get; set; }

    public string Currency { get; set; } = null!;

    public Guid? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNotes { get; set; }

    public Guid? RefundId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual PurchaseDataModel Purchase { get; set; } = null!;

    public virtual RefundDataModel? Refund { get; set; }
}
