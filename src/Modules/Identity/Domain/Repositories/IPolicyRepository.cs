using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IPolicyRepository : IRepository<Policy>
{
    Task<IReadOnlyCollection<ManagedPolicy>?> GetManagedPoliciesForPrincipal(Guid id);
}

public interface IManagedPolicyRepository : IRepository<ManagedPolicy>
{
    Task AssignPolicyToPrincipal(Guid principalId, Guid managedPolicyId);
    Task RemovePolicyFromPrincipal(Guid principalId, Guid managedPolicyId);
}

public interface IPrincipalRepository : IRepository<Principal>
{
}
