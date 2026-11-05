using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class RedemptionAuditLogRepository : IRedemptionAuditLogRepository
{
    private readonly AppDbContext _context;

    public RedemptionAuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RedemptionAuditLog log, CancellationToken ct = default)
        => await _context.RedemptionAuditLogs.AddAsync(log, ct);
}
