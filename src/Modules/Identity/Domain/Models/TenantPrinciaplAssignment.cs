using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class TenantUserPrinciaplAssignment : AggregateRoot, IDomainTenantOwned
{
    private TenantUserPrinciaplAssignment(Guid id, Guid tenantId, TenantUser tenantUser, Principal principal, ResourceArn arn)
        : base(id)
    {
        TenantId = tenantId;
        TenantUser = tenantUser;
        Principal = principal;
        Resource = arn;
    }
    private TenantUserPrinciaplAssignment() // ef core
    {
    }
    //for example, a student in a course can have a principal assignment with the scope of that course, and another assignment with the scope of another course, both with the same role principal but different scopes, this allows for more flexible and fine-grained access control.
    //imaging having a student role principal for each course, it would be a mess, instead we have one student role principal and assign it to the tenant user with different scopes for each course.
    //and the principal here should be global and tenant agnostic with TenantId = null and ResourceId = null, and the scope of the assignment will determine the effective scope of the principal for that tenant user.

    public Guid TenantId { get; private set; }

    public TenantUser TenantUser { get; private set; }
    public Principal Principal { get; private set; }
    public ResourceArn Resource { get; private set; }

    public IReadOnlyCollection<IPolicy> Policies => Principal.Policies;

    public static ErrorOr<TenantUserPrinciaplAssignment> Create(Guid tenantId, TenantUser tenantUser, Principal principal, string resourceArn)
    {
        if (principal.PrincipalScope is not null)
        {
            return Error.Validation("Assignment.Principal", "Principal used for assignment must not have a pre-defined scope (PrincipalScope must be null).");
        }

        ErrorOr<ResourceArn> resource = ResourceArn.Create(resourceArn);
        if (resource.IsError)
        {
            return resource.Errors;
        }
        return new TenantUserPrinciaplAssignment(Guid.NewGuid(), tenantId, tenantUser, principal, resource.Value);
    }
}
