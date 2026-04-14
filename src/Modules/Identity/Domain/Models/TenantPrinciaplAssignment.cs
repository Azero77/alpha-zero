using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class TenantPrinciaplAssignment : AggregateRoot, IDomainTenantOwned
{
    private TenantPrinciaplAssignment(Guid id, Guid tenantId, TenantUser tenantUser, PrincipalTemplate principal, ResourcePattern scope)
        : base(id)
    {
        TenantId = tenantId;
        TenantUser = tenantUser;
        Principal = principal;
        ScopePattern = scope;
    }

    public Guid TenantId { get; private set; }
    public TenantUser TenantUser { get; private set; }
    public PrincipalTemplate Principal { get; private set; }
    
    // Boundary: Changed from ResourceArn to ResourcePattern to allow wildcards in enrollments
    public ResourcePattern ScopePattern { get; private set; }

    public static ErrorOr<TenantPrinciaplAssignment> Create(Guid tenantId, TenantUser tenantUser, PrincipalTemplate principal, string scopePattern)
    {
        var patternResult = ResourcePattern.Create(scopePattern);
        if (patternResult.IsError) return patternResult.Errors;

        return new TenantPrinciaplAssignment(Guid.NewGuid(), tenantId, tenantUser, principal, patternResult.Value);
    }
}
