using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class ReconciliationFindingDataModel
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public string Severity { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public Guid? AggregateId { get; set; }

    public string Detail { get; set; } = null!;

    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public virtual ReconciliationRunDataModel Run { get; set; } = null!;
}
