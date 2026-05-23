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

public class PolicyTemplateStatement
{
    public string Sid { get; private set; } = string.Empty;
    public List<string> Actions { get; private set; } = new List<string>();

    public bool Effect { get; private set; }

    public IConditionNode? Condition { get; private set; }
    private PolicyTemplateStatement() { } // EF and JSON

    public PolicyTemplateStatement(string sid, List<string> actions, bool effect, IConditionNode? condition = null)
    {
        Sid = sid;
        Actions = actions;
        Effect = effect;
        Condition = condition;
    }
}
public enum Operator
{
    StringEquals,
    StringNotEquals,
    StringLike,
    StringNotLike,
    NumericEquals,
    NumericNotEquals,
    NumericLessThan,
    NumericLessThanEquals,
    NumericGreaterThan,
    NumericGreaterThanEquals,
    DateEquals,
    DateNotEquals,
    DateLessThan,
    DateLessThanEquals,
    DateGreaterThan,
    DateGreaterThanEquals,
    Bool, 
    In,
    NotIn
}

public enum ConditionType
{
    And,
    Or,
    Not,
    Statement
}
public interface IConditionNode
{
    public ConditionType Type { get; }
}

public record ConditionNode(string Property, Operator Operator, JsonElement Value) : IConditionNode
{
    public ConditionType Type => ConditionType.Statement;
}

public record AndNode(List<IConditionNode> Conditions) : IConditionNode
{
    public ConditionType Type => ConditionType.And;
}

public record OrNode(List<IConditionNode> Conditions) : IConditionNode
{
    public ConditionType Type => ConditionType.Or;
}

public record NotNode(IConditionNode Condition) : IConditionNode
{
    public ConditionType Type => ConditionType.Not;
}