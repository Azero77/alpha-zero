using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

namespace AlphaZero.Modules.Identity.Infrastructure.Models;

public class PrincipalPolicyAssignment
{
    public Guid PrincipalId { get; set; }
    public Guid ManagedPolicyId { get; set; }

    public ManagedPolicy ManagedPolicy { get; set; } = null!;
}
