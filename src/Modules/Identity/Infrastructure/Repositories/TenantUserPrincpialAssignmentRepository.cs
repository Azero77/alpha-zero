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

        var query = from assignment in _context.TenantPrinciaplAssignments.Include(a => a.TenantUser)
                    join principalData in _context.Principals.Include(p => p.ManagedPolicies)
                    on EF.Property<Guid>(assignment, "PrincipalId") equals principalData.Id
                    where assignment.TenantUser.Id == tenantUserId && 
                          (assignment.Resource == arnResult.Value || resourceArn.StartsWith(assignment.Resource.Value))
                    select new { assignment, principalData };

        var result = await query.FirstOrDefaultAsync();

        if (result == null) return null;

        return HydrateAssignment(result.assignment, result.principalData);
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
