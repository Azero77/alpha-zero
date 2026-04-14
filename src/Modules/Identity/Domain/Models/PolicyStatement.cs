using System.Text.Json;
using AlphaZero.Shared.Domain;

namespace AlphaZero.Modules.Identity.Domain.Models;

public record PolicyStatement
{
    public string Sid { get; init; } = string.Empty;
    public List<string> Actions { get; init; } = new List<string>();
    public bool Effect { get; init; } // true for Allow, false for Deny
    
    // Boundary: Changed from List<string> to List<ResourcePattern>
    public List<ResourcePattern> Resources { get; init; } = new List<ResourcePattern>();
    public JsonElement? Condition { get; init; }

    public PolicyStatement(string sid, List<string> actions, bool effect, List<ResourcePattern> resources, JsonElement? condition = null)
    {
        Sid = sid;
        Actions = actions;
        Effect = effect;
        Resources = resources;
        Condition = condition;
    }
}

public record PolicyTemplateStatement(string Sid, List<string> Actions, bool Effect);
