using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using FluentAssertions;
using NSubstitute;
using System.Text.Json;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Services;

public class ConditionEvaluatorServiceTests
{
    private readonly AuthorizationContext _context;
    private readonly ConditionEvaluatorService _evaluator;

    public ConditionEvaluatorServiceTests()
    {
        _context = new AuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthenticationMethod = "Principal",
            RequiredPermission = "courses:View",
            ResourcePath = "course/101",
            ResourceType = ResourceType.Courses
        };
        var conditionRepository = Substitute.For<IConditionRepository>();
        _evaluator = new ConditionEvaluatorService(_context, conditionRepository);
    }

    [Fact]
    public void Evaluate_StringEquals_ShouldReturnSuccess_WhenMatch()
    {
        // Arrange
        var condition = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/101\"").RootElement);

        // Act
        var result = _evaluator.Evaluate(condition);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_StringEquals_ShouldReturnError_WhenNoMatch()
    {
        // Arrange
        var condition = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/102\"").RootElement);

        // Act
        var result = _evaluator.Evaluate(condition);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Condition.NotMet");
    }

    [Fact]
    public void Evaluate_NumericEquals_ShouldReturnSuccess_WhenMatch()
    {
        // Arrange
        // We use ResourceType which is an enum (int)
        var condition = new ConditionNode("ResourceType", Operator.NumericEquals, JsonDocument.Parse("0").RootElement); // Courses = 0

        // Act
        var result = _evaluator.Evaluate(condition);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_AndNode_ShouldReturnSuccess_WhenAllMatch()
    {
        // Arrange
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/101\"").RootElement);
        var c2 = new ConditionNode("RequiredPermission", Operator.StringEquals, JsonDocument.Parse("\"courses:View\"").RootElement);
        var andNode = new AndNode(new List<IConditionNode> { c1, c2 });

        // Act
        var result = _evaluator.Evaluate(andNode);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OrNode_ShouldReturnSuccess_WhenOneMatches()
    {
        // Arrange
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"WRONG\"").RootElement);
        var c2 = new ConditionNode("RequiredPermission", Operator.StringEquals, JsonDocument.Parse("\"courses:View\"").RootElement);
        var orNode = new OrNode(new List<IConditionNode> { c1, c2 });

        // Act
        var result = _evaluator.Evaluate(orNode);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NotNode_ShouldReturnSuccess_WhenConditionFails()
    {
        // Arrange
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"WRONG\"").RootElement);
        var notNode = new NotNode(c1);

        // Act
        var result = _evaluator.Evaluate(notNode);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_VariableReference_ShouldReturnSuccess_WhenMatch()
    {
        // Arrange
        // We compare ResourcePath with itself using a variable reference
        var condition = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"$ResourcePath\"").RootElement);

        // Act
        var result = _evaluator.Evaluate(condition);

        // Assert
        result.IsError.Should().BeFalse();
    }
    
    [Fact]
    public void Evaluate_InOperator_ShouldReturnSuccess_WhenValueInArray()
    {
        // Arrange
        var condition = new ConditionNode("ResourcePath", Operator.In, JsonDocument.Parse("[\"course/100\", \"course/101\", \"course/102\"]").RootElement);

        // Act
        var result = _evaluator.Evaluate(condition);

        // Assert
        result.IsError.Should().BeFalse();
    }
}
