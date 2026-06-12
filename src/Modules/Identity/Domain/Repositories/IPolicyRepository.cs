using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using System.Linq.Expressions;

namespace AlphaZero.Modules.Identity.Domain.Repositories;

public interface IManagedPolicyRepository : IRepository<ManagedPolicy>
{
    Task AssignPolicyToPrincipal(Guid principalId, Guid managedPolicyId);
    Task RemovePolicyFromPrincipal(Guid principalId, Guid managedPolicyId);
}

public interface IPrincipalRepository
{
    Task<IReadOnlyCollection<Principal>> GetPrincipalsByResourceAsync(Guid resourceId, string resourceType, CancellationToken ct = default);

    Task<Principal?> GetById(Guid id, CancellationToken token = default);
    Task<Principal?> GetFirst(Expression<Func<Principal?, bool>> predicate, CancellationToken token = default);
    void Add(Principal entity);
    void Update(Principal entity);
    void Remove(Principal entity);
    Task<bool> Any(Expression<Func<Principal, bool>> predicate, CancellationToken token = default);
}


public interface ITenantUserPrincipalAssignmentRepository : IRepository<TenantUserPrincipalAssignment>
{
    Task<List<TenantUserPrincipalAssignment>> GetActiveAssignments(Guid tenantUserId, string? resourceArn = null);
}
