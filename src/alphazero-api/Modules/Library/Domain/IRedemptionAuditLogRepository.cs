namespace AlphaZero.Modules.Library.Domain;

public interface IRedemptionAuditLogRepository
{
    Task AddAsync(RedemptionAuditLog log, CancellationToken ct = default);
}
