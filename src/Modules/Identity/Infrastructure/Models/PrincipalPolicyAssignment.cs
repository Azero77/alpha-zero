using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

namespace AlphaZero.Modules.Identity.Infrastructure.Models;

public class PrincipalPolicyAssignment
{
    public Guid PrincipalId { get; set; }
    public Guid ManagedPolicyId { get; set; }

    public Principal Principal { get; set; } = null!;
    public ManagedPolicy ManagedPolicy { get; set; } = null!;
}