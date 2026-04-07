using AlphaZero.Modules.Identity.Domain.Models;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IPolicyRepository
{
    Task<IReadOnlyCollection<ManagedPolicy>?> GetManagedPoliciesForPrincipal(Guid id);
    Task<Principal?> GetPrincipal(Guid principalId);
}
