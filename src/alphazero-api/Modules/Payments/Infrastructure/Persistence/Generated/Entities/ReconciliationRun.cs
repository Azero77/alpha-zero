using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ReconciliationRunDataModel
{
    public Guid Id { get; set; }

    public Guid? ProviderAccountId { get; set; }

    public string Kind { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string Status { get; set; } = null!;

    public string? Counts { get; set; }

    public string TriggeredBy { get; set; } = null!;

    public virtual ProviderAccountDataModel? ProviderAccount { get; set; }

    public virtual ICollection<ReconciliationFindingDataModel> ReconciliationFindings { get; set; } = new List<ReconciliationFindingDataModel>();
}
