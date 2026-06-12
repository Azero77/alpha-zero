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

public class TenantUserPrincipalAssignmentRepository : BaseRepository<AppDbContext, TenantUserPrincipalAssignment>, ITenantUserPrincipalAssignmentRepository
{
    public TenantUserPrincipalAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<List<TenantUserPrincipalAssignment>> GetActiveAssignments(Guid tenantUserId, string? resourceArn = null)
    {
        var assignments = await _context.TenantPrincipalAssignments
            .Include(a => a.TenantUser)
            .Where(a => a.TenantUser.Id == tenantUserId)
            .ToListAsync();

        List<TenantUserPrincipalAssignment> matchedAssignments;
        if (string.IsNullOrEmpty(resourceArn) || resourceArn == "*")
        {
            matchedAssignments = assignments;
        }
        else
        {
            var arnResult = ResourceArn.Create(resourceArn);
            if (arnResult.IsError) return new List<TenantUserPrincipalAssignment>();
            var targetArn = arnResult.Value;

            matchedAssignments = assignments.Where(a => 
                a.Resource.TenantIdString.Equals(targetArn.TenantIdString, StringComparison.OrdinalIgnoreCase) &&
                (targetArn.ResourcePath.Equals(a.Resource.ResourcePath, StringComparison.OrdinalIgnoreCase) ||
                 targetArn.ResourcePath.StartsWith(a.Resource.ResourcePath + "/", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var resultList = new List<TenantUserPrincipalAssignment>();
        foreach (var matched in matchedAssignments)
        {
            var principalData = await _context.Principals
                .Include(p => p.ManagedPolicies)
                .FirstOrDefaultAsync(p => p.Id == matched.PrincipalId);

            if (principalData != null)
            {
                resultList.Add(HydrateAssignment(matched, principalData));
            }
        }

        return resultList;
    }

    private TenantUserPrincipalAssignment HydrateAssignment(TenantUserPrincipalAssignment assignment, PrincipalDataModel principalData)
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
        var principalField = typeof(TenantUserPrincipalAssignment).GetProperty("Principal", BindingFlags.Public | BindingFlags.Instance);
        principalField?.SetValue(assignment, principal);

        return assignment;
    }
}
