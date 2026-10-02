using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Tenants.Domain;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<bool> IsSubdomainUniqueAsync(string subdomain, CancellationToken ct = default);
    Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default);
}
