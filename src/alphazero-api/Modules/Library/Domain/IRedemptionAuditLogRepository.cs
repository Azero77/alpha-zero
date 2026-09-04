using AlphaZero.Shared.Application;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Library.Domain;

public interface IRedemptionAuditLogRepository
{
    Task AddAsync(RedemptionAuditLog log, CancellationToken ct = default);
    
    Task<PagedResult<RedemptionAuditLog>> GetPagedAsync(
        Guid? libraryId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
