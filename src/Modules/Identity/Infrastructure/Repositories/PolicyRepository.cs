using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class PolicyRepository : BaseRepository<AppDbContext, Policy>, IPolicyRepository
{
    public PolicyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyCollection<ManagedPolicy>?> GetManagedPoliciesForPrincipal(Guid id)
    {
        var assignments = await _context.PrincipalPolicyAssignments
            .Include(p => p.ManagedPolicy)
            .Where(p => p.PrincipalId == id)
            .ToListAsync();

        return assignments.Select(a => a.ManagedPolicy).ToList();
    }
}

public class ManagedPolicyRepository : BaseRepository<AppDbContext, ManagedPolicy>, IManagedPolicyRepository
{
    public ManagedPolicyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task AssignPolicyToPrincipal(Guid principalId, Guid managedPolicyId)
    {
        var assignment = new PrincipalPolicyAssignment
        {
            PrincipalId = principalId,
            ManagedPolicyId = managedPolicyId
        };

        await _context.PrincipalPolicyAssignments.AddAsync(assignment);
    }

    public async Task RemovePolicyFromPrincipal(Guid principalId, Guid managedPolicyId)
    {
        var assignment = await _context.PrincipalPolicyAssignments
            .FirstOrDefaultAsync(a => a.PrincipalId == principalId && a.ManagedPolicyId == managedPolicyId);

        if (assignment != null)
        {
            _context.PrincipalPolicyAssignments.Remove(assignment);
        }
    }
}

public class PrincipalRepository : BaseRepository<AppDbContext, Principal>, IPrincipalRepository
{
    public PrincipalRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<Principal?> GetById(Guid id)
    {
        return await _context.Principals
            .Include(p => p.InlinePolicies)
            .Include(t => t.ManagedPolicies)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlyCollection<Principal>> GetPrincipalsByResourceAsync(Guid resourceId, ResourceType resourceType, CancellationToken ct = default)
    {
        return await _context.Principals
            .Include(p => p.InlinePolicies)
            .Where(p => p.ResourceId == resourceId && p.ScopeResourceType == resourceType)
            .ToListAsync(ct);
    }
}
