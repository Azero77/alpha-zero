namespace AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;

public record RedemptionAuditLogDto(
    Guid Id,
    Guid AccessCodeId,
    Guid? LibraryId,
    Guid RedeemedByUserId,
    string StrategyId,
    string TargetResourceArn,
    DateTime RedeemedAt,
    string? IpAddress,
    string? DeviceFingerprint);
