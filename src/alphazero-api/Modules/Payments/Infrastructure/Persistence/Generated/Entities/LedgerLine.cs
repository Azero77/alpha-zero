using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class LedgerLineDataModel
{
    public Guid Id { get; set; }

    public Guid EntryId { get; set; }

    public Guid AccountId { get; set; }

    public string Direction { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public Guid? TenantId { get; set; }

    public virtual LedgerAccountDataModel Account { get; set; } = null!;

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual LedgerEntryDataModel Entry { get; set; } = null!;
}
