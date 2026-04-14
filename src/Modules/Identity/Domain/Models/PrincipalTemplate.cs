using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class PrincipalTemplate : Entity
{
    public PrincipalTemplate(Guid id, string? name, PrincipalType principalType)
        : base(id)
    {
        Name = name;
        PrincipalType = principalType;
    }

    public string? Name { get; private set; } = string.Empty;
    public PrincipalType PrincipalType { get; private set; }
    public List<ManagedPolicy> ManagedPolicies { get; private set; } = new List<ManagedPolicy>(); //a normal principal can have inline policies and managed policies, while a managed principal can only have managed policies, and the managed policies assigned to a managed principal will be applied to all the principals that are assigned to it, this allows for more flexible and reusable policy management, for example, we can have a managed principal for students and assign it to all student principals with different scopes for each course, and then we can manage the policies for all students in one place by managing the policies of the managed principal.
} 
