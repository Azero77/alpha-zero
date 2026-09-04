using AlphaZero.Modules.Tenants.Application.Tenants.Queries.GetTenant;
using AlphaZero.Shared.Queries;

namespace AlphaZero.Modules.Tenants.Application.Queries;

public interface ITenantQueryService
{
    Task<PagedResult<TenantDto>> ListTenantsAsync(string? search, int page, int perPage, CancellationToken cancellationToken = default);
}
