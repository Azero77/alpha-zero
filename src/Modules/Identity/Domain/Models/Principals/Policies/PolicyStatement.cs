using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;

public class PolicyStatement
{
    public string Sid { get; private set; } = string.Empty;
    public List<string> Actions { get; private set; } = new List<string>();
    public bool Effect { get; private set; } // true for Allow, false for Deny
    public List<ResourcePattern> Resources { get; private set; } = new List<ResourcePattern>();
    public IConditionNode? Condition { get; private set; }

    private PolicyStatement() { } // EF and JSON

    public PolicyStatement(string sid, List<string> actions, bool effect, List<ResourcePattern> resources, IConditionNode? condition = null)
    {
        Sid = sid;
        Actions = actions;
        Effect = effect;
        Resources = resources;
        Condition = condition;
    }
}

public class ManagedPolicyStatement
{
    public string Sid { get; private set; } = string.Empty;
    public List<string> Actions { get; private set; } = new List<string>();

    public bool Effect { get; private set; }

    public IConditionNode? Condition { get; private set; }
    private ManagedPolicyStatement() { } // EF and JSON

    public ManagedPolicyStatement(string sid, List<string> actions, bool effect, IConditionNode? condition = null)
    {
        Sid = sid;
        Actions = actions;
        Effect = effect;
        Condition = condition;
    }
}
