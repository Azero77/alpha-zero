using AlphaZero.Modules.Library.Domain;
using AlphaZero.Modules.Library.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Library.Infrastructure.Repositories;

public class AccessCodeRepository : BaseRepository<AppDbContext, AccessCode>, IAccessCodeRepository
{
    public AccessCodeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AccessCode?> GetByHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        return await _context.AccessCodes
            .FirstOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);
    }
}
