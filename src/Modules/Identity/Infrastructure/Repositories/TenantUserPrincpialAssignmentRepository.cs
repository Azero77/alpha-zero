using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Infrastructure.Persistance;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AlphaZero.Modules.Identity.Infrastructure.Repositories;

public class TenantUserPrincpialAssignmentRepository : BaseRepository<AppDbContext, TenantUserPrinciaplAssignment>, ITenantUserPrincpialAssignmentRepository
{
    public TenantUserPrincpialAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<TenantUserPrinciaplAssignment?> Get(Guid tenantUserId, string resourceArn)
    {
        var arnResult = ResourceArn.Create(resourceArn);
        if (arnResult.IsError) return null;

        // Fetch assignments for the user. We filter by TenantUserId server-side.
        // For the Resource comparison, since it has a conversion, EF Core might struggle with complex LINQ.
        // We'll fetch all assignments for the user (usually very few) and filter the rest in memory.
        var assignments = await _context.TenantPrinciaplAssignments
            .Include(a => a.TenantUser)
            .Where(a => a.TenantUser.Id == tenantUserId)
            .ToListAsync();

        var matchedAssignment = assignments.FirstOrDefault(a => 
            a.Resource.Value == arnResult.Value.Value || resourceArn.StartsWith(a.Resource.Value));

        if (matchedAssignment == null) return null;

        // Join manually with PrincipalDataModel
        var principalData = await _context.Principals
            .Include(p => p.ManagedPolicies)
            .FirstOrDefaultAsync(p => p.Id == matchedAssignment.PrincipalId);

        if (principalData == null) return null;

        return HydrateAssignment(matchedAssignment, principalData);
    }

    private TenantUserPrinciaplAssignment HydrateAssignment(TenantUserPrinciaplAssignment assignment, PrincipalDataModel principalData)
    {
        var principalResult = Principal.Create(
            principalData.Id,
            principalData.Username,
            principalData.PasswordHash,
            principalData.Name,
            principalData.PrincipalType,
            principalData.PrincipalScopePattern,
            principalData.TenantId);

        if (principalResult.IsError) return assignment;

        var principal = principalResult.Value;
        
        var allPolicies = principalData.InlinePolicies.Cast<IPolicy>()
            .Concat(principalData.ManagedPolicies.Cast<IPolicy>());
        
        principal.LoadPolicies(allPolicies);

        // Use reflection to set the private Principal property
        var principalField = typeof(TenantUserPrinciaplAssignment).GetProperty("Principal", BindingFlags.Public | BindingFlags.Instance);
        principalField?.SetValue(assignment, principal);

        return assignment;
    }
}
