using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class PaymentProviderDataModel
{
    public string Name { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ProviderAccountDataModel> ProviderAccounts { get; set; } = new List<ProviderAccountDataModel>();

    public virtual ICollection<WebhookEventDataModel> WebhookEvents { get; set; } = new List<WebhookEventDataModel>();
}
