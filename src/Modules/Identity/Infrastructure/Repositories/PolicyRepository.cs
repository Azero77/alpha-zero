using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class PolicyRepository : IPolicyRepository
{
    private readonly AppDbContext _context;

    public PolicyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<ManagedPolicy>?> GetManagedPoliciesForPrincipal(Guid id)
    {
        var assignments = await _context.PrincipalPolicyAssignments
            .Include(p => p.ManagedPolicy)
            .Where(p => p.PrincipalId == id)
            .ToListAsync();

        return assignments.Select(a => a.ManagedPolicy).ToList();

    }

    public async Task<Principal?> GetPrincipal(Guid principalId)
    {
        return await _context.Principals.Include(p => p.InlinePolicies)
            .FirstOrDefaultAsync(p => p.Id == principalId);
    }
}
