using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class DisputeDataModel
{
    public Guid Id { get; set; }

    public Guid PurchaseId { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProviderAccountId { get; set; }

    public string ExternalDisputeId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public decimal ProviderDisputeFee { get; set; }

    public DateTime OpenedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public string? OutcomeNotes { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual ICollection<LedgerEntryDataModel> LedgerEntries { get; set; } = new List<LedgerEntryDataModel>();

    public virtual ProviderAccountDataModel ProviderAccount { get; set; } = null!;

    public virtual PurchaseDataModel Purchase { get; set; } = null!;
}
