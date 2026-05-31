using System.Text.Json;
namespace AlphaZero.Modules.Identity.Domain.Models.Principals;

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
    Statement,
    Reference
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

public record ConditionReferenceNode(string ReferenceName) : IConditionNode
{
    public ConditionType Type => ConditionType.Reference;
}
