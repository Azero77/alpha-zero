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
    public List<ManagedPolicy> ManagedPolicies { get; private set; } = new List<ManagedPolicy>();

    public virtual IEnumerable<PolicyStatement> GetEffectiveStatements(string? scope, Guid tenantId)
    {
        var effectiveScope = scope ?? "az:*";
        return ManagedPolicies.SelectMany(mp => mp.Build(effectiveScope, tenantId).Statements);
    }
} 
