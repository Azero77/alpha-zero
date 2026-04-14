using System.Text.Json;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Domain.Models;

public class PolicyStatement
{
    public string Sid { get; private set; } = string.Empty;
    public List<string> Actions { get; private set; } = new List<string>();
    public bool Effect { get; private set; } // true for Allow, false for Deny
    
    // Boundary: Changed from List<string> to List<ResourcePattern>
    public List<ResourcePattern> Resources { get; private set; } = new List<ResourcePattern>();
    public JsonElement? Condition { get; private set; }

    private PolicyStatement() { } // EF and JSON

    public PolicyStatement(string sid, List<string> actions, bool effect, List<ResourcePattern> resources, JsonElement? condition = null)
    {
        Sid = sid;
        Actions = actions;
        Effect = effect;
        Resources = resources;
        Condition = condition;
    }
}

public class PolicyTemplateStatement
{
    public string Sid { get; private set; } = string.Empty;
    public List<string> Actions { get; private set; } = new List<string>();
    public bool Effect { get; private set; }

    private PolicyTemplateStatement() { } // EF and JSON

    public PolicyTemplateStatement(string sid, List<string> actions, bool effect)
    {
        Sid = sid;
        Actions = actions;
        Effect = effect;
    }
}
