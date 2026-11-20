using System;
using System.Collections.Generic;

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Generated.Entities;

public partial class AcademyPaymentSettingDataModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public bool OnlineSellingEnabled { get; set; }

    public decimal CommissionShare { get; set; }

    public short? RefundWindowDays { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
