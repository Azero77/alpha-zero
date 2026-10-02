using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;
using AlphaZero.Shared.Application;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class AccessCodeRepository : BaseRepository<AppDbContext, AccessCode>, IAccessCodeRepository
{
    public AccessCodeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<int> MarkBatchAsDistributedAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<AccessCode>()
            .Where(x => x.BatchId == batchId && x.Status == AccessCodeStatus.Minted)
            .ExecuteUpdateAsync(setter => setter.SetProperty(s => s.Status, AccessCodeStatus.Distributed), cancellationToken);
    }

    public async Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        return await _context.AccessCodes
            .FirstOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);
    }
}
