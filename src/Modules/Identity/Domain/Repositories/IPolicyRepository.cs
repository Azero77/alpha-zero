using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IManagedPolicyRepository : IRepository<ManagedPolicy>
{
    Task AssignPolicyToPrincipal(Guid principalId, Guid managedPolicyId);
    Task RemovePolicyFromPrincipal(Guid principalId, Guid managedPolicyId);
}

public interface IPrincipalRepository
{
    Task<IReadOnlyCollection<Principal>> GetPrincipalsByResourceAsync(Guid resourceId, ResourceType resourceType, CancellationToken ct = default);
}


public interface ITenantUserPrincpialAssignmentRepository : IRepository<TenantUserPrinciaplAssignment>
{
    Task<TenantUserPrinciaplAssignment?> Get(Guid tenantUserId, string resourceArn);
}
