using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class LedgerAccountDataModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Currency { get; set; } = null!;

    public Guid? TenantId { get; set; }

    public Guid? ProviderAccountId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual CurrencyDataModel CurrencyNavigation { get; set; } = null!;

    public virtual ICollection<LedgerLineDataModel> LedgerLines { get; set; } = new List<LedgerLineDataModel>();

    public virtual PaymentReviewCaseDataModel? PaymentReviewCasePayableAccount { get; set; }

    public virtual ICollection<PaymentReviewCaseDataModel> PaymentReviewCaseSourceAccounts { get; set; } = new List<PaymentReviewCaseDataModel>();

    public virtual ProviderAccountDataModel? ProviderAccount { get; set; }

    public virtual ICollection<SettlementDataModel> SettlementPayableAccounts { get; set; } = new List<SettlementDataModel>();

    public virtual ICollection<SettlementDataModel> SettlementSourceAccounts { get; set; } = new List<SettlementDataModel>();
}
