using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Infrastructure.Persistance;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Tenants.Infrastructure.Repositories;

public class TenantRepository : BaseRepository<AppDbContext, Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<bool> IsSubdomainUniqueAsync(string subdomain, CancellationToken ct = default)
    {
        return !await _context.Tenants.AnyAsync(x => x.Subdomain == subdomain.ToLowerInvariant(), ct);
    }

    public async Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        return await _context.Tenants.FirstOrDefaultAsync(x => x.Subdomain == subdomain.ToLowerInvariant(), ct);
    }
}
