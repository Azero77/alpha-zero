using AlphaZero.Shared.Domain;
using ErrorOr;

namespace AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

public class ManagedPolicy : Entity, IPolicy
{
    public PolicyType Type => PolicyType.Managed;
    public string Name { get; private set; } = string.Empty;
    public List<ManagedPolicyStatement> Statements { get; private set; } = new List<ManagedPolicyStatement>();

    private ManagedPolicy() { } // EF and JSON

    public ManagedPolicy(Guid id, string name, List<ManagedPolicyStatement> statements) : base(id)
    {
        Name = name;
        Statements = statements;
    }

    public ErrorOr<IReadOnlyCollection<PolicyStatement>> GetPolicyStatements(string? scope, Guid tenantId)
    {
        if (scope is null)
            return Error.Validation("Identity.Policy.Validation", "Scope is required.");

        var patternResult = ResourcePattern.Create(scope);
        if (patternResult.IsError)
            return patternResult.Errors;

        var result = new List<PolicyStatement>();

        foreach (var item in Statements)
        {
            result.Add(new PolicyStatement(
                item.Sid, 
                item.Actions, 
                item.Effect, 
                new List<ResourcePattern> { patternResult.Value }, 
                item.Condition));
        }

        return result;
    }
}
