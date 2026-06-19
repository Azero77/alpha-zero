using AlphaZero.Modules.Tenants.Application.Queries;
using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Modules.Tenants.Domain;
using AlphaZero.Modules.Tenants.Infrastructure.Persistance;
using AlphaZero.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AlphaZero.Modules.Tenants.Infrastructure.Queries;

public class TenantQueryService : ITenantQueryService
{
    private readonly AppDbContext _context;

    public TenantQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<TenantDto>> ListTenantsAsync(string? search, int page, int perPage, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Tenant>().AsNoTracking();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Name.Contains(search) || t.Subdomain.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(t => new TenantDto(
                t.Id,
                t.Name,
                t.Subdomain,
                t.LogoUrl,
                t.PrimaryColor,
                t.SecondaryColor,
                t.Status.ToString(),
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<TenantDto>(items, totalCount, page, perPage);
    }
}
