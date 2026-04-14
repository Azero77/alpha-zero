using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class TenantPrinciaplAssignment : AggregateRoot, IDomainTenantOwned
{
    private TenantPrinciaplAssignment(Guid id,Guid tenantId, TenantUser tenantUser, PrincipalTemplate principal, string scope)
        : base(id)
    {
        TenantId = tenantId;
        TenantUser = tenantUser;
        Principal = principal;
        Scope = scope;
    }
    //for example, a student in a course can have a principal assignment with the scope of that course, and another assignment with the scope of another course, both with the same role principal but different scopes, this allows for more flexible and fine-grained access control.
    //imaging having a student role principal for each course, it would be a mess, instead we have one student role principal and assign it to the tenant user with different scopes for each course.
    //and the principal here should be global and tenant agnostic with TenantId = null and ResourceId = null, and the scope of the assignment will determine the effective scope of the principal for that tenant user.

    public Guid TenantId { get; private set; }

    public TenantUser TenantUser { get; private set; }
    public PrincipalTemplate Principal { get; private set; }
    public string Scope { get; private set; }

    public static ErrorOr<TenantPrinciaplAssignment> Create(Guid tenantId, TenantUser tenantUser, PrincipalTemplate principal, string scope)
    {
        //validating scope
        var isValidScope = ResourceArn.IsValidPattern(scope);

        if (isValidScope.IsError) return isValidScope.Errors;
        //we will accept a scope ending with /* or * , but will remove it for consistency
        var newScope = scope.TrimEnd('*','/');
        return new TenantPrinciaplAssignment(Guid.NewGuid(), tenantId, tenantUser, principal, newScope);
    }

}