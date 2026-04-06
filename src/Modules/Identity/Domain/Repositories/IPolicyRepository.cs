using AlphaZero.Modules.Identity.Domain.Models;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IPolicyRepository
{
    Task<IReadOnlyCollection<Policy>?> GetManagedPoliciesForPrincipal(Guid id);
    Task<IReadOnlyCollection<Policy>?> GetPolicies(Guid principalId,Guid tenantId);
    Task<Principal?> GetPrincipal(Guid principalId);
}
