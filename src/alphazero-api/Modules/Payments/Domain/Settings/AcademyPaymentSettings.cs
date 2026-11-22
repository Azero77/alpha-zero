using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Payments.Domain.Settings;

public sealed class AcademyPaymentSettings : AggregateRoot
{
    private AcademyPaymentSettings() { }
    private AcademyPaymentSettings(Guid id, Guid tenantId, bool onlineSellingEnabled, decimal commissionShare,
        short? refundWindowDays, Guid? updatedByUserId, DateTime createdAt, DateTime? updatedAt) : base(id)
    {
        TenantId = tenantId;
        OnlineSellingEnabled = onlineSellingEnabled;
        CommissionShare = commissionShare;
        RefundWindowDays = refundWindowDays;
        UpdatedByUserId = updatedByUserId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid TenantId { get; private set; }
    public bool OnlineSellingEnabled { get; private set; }
    public decimal CommissionShare { get; private set; }
    public short? RefundWindowDays { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static ErrorOr<AcademyPaymentSettings> CreatePolicy(Guid id, Guid tenantId, bool enabled,
        decimal commissionShare, short? refundWindowDays, Guid actorUserId, DateTime now)
    {
        var validation = Validate(tenantId, commissionShare, refundWindowDays, actorUserId);
        if (validation.IsError) return validation.Errors;
        var settings = new AcademyPaymentSettings(id, tenantId, enabled, commissionShare, refundWindowDays, actorUserId, now, now);
        settings.AddDomainEvent(new AcademyPaymentSettingsUpdatedDomainEvent(id, tenantId, enabled, commissionShare, refundWindowDays, actorUserId, now));
        return settings;
    }

    public static AcademyPaymentSettings Reconstitute(Guid id, Guid tenantId, bool enabled, decimal commissionShare,
        short? refundWindowDays, Guid? updatedByUserId, DateTime createdAt, DateTime? updatedAt) =>
        new(id, tenantId, enabled, commissionShare, refundWindowDays, updatedByUserId, createdAt, updatedAt);

    public ErrorOr<Success> UpdatePolicy(bool enabled, decimal commissionShare, short? refundWindowDays,
        Guid actorUserId, DateTime now)
    {
        var validation = Validate(TenantId, commissionShare, refundWindowDays, actorUserId);
        if (validation.IsError) return validation;
        OnlineSellingEnabled = enabled;
        CommissionShare = commissionShare;
        RefundWindowDays = refundWindowDays;
        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
        AddDomainEvent(new AcademyPaymentSettingsUpdatedDomainEvent(Id, TenantId, enabled, commissionShare, refundWindowDays, actorUserId, now));
        return Result.Success;
    }

    private static ErrorOr<Success> Validate(Guid tenantId, decimal commissionShare, short? refundWindowDays, Guid actorUserId)
    {
        if (tenantId == Guid.Empty || actorUserId == Guid.Empty)
            return Error.Validation("AcademyPaymentSettings.Identity", "Academy and actor ids are required.");
        if (commissionShare is < 0 or > 1)
            return Error.Validation("AcademyPaymentSettings.CommissionShare", "Commission Share must be between zero and one.");
        if (refundWindowDays is < 0 or > 365)
            return Error.Validation("AcademyPaymentSettings.RefundWindow", "Refund window must be between zero and 365 days.");
        return Result.Success;
    }
}

public sealed record AcademyPaymentSettingsUpdatedDomainEvent(Guid SettingsId, Guid TenantId, bool OnlineSellingEnabled,
    decimal CommissionShare, short? RefundWindowDays, Guid ActorUserId, DateTime OccurredAt) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccuredOn => OccurredAt;
}
