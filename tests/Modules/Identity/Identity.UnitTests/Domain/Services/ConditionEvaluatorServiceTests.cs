using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
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
            ResourceType = "course"
        };
        var conditionRepository = Substitute.For<IConditionRepository>();
        var operationEvaluators = Enumerable.Empty<IOperationEvaluator>();
        _evaluator = new ConditionEvaluatorService(conditionRepository, operationEvaluators);
    }

    [Fact]
    public async Task Evaluate_StringEquals_ShouldReturnSuccess_WhenMatch()
    {
        // Arrange
        var condition = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/101\"").RootElement);

        // Act
        var result = await _evaluator.Evaluate(condition, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_StringEquals_ShouldReturnError_WhenNoMatch()
    {
        // Arrange
        var condition = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/102\"").RootElement);

        // Act
        var result = await _evaluator.Evaluate(condition, _context);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Condition.NotMet");
    }

    [Fact]
    public async Task Evaluate_StringEquals_ResourceType_ShouldReturnSuccess_WhenMatch()
    {
        // Arrange
        var condition = new ConditionNode("ResourceType", Operator.StringEquals, JsonDocument.Parse("\"course\"").RootElement);

        // Act
        var result = await _evaluator.Evaluate(condition, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_AndNode_ShouldReturnSuccess_WhenAllMatch()
    {
        // Arrange
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"course/101\"").RootElement);
        var c2 = new ConditionNode("RequiredPermission", Operator.StringEquals, JsonDocument.Parse("\"courses:View\"").RootElement);
        var andNode = new AndNode(new List<IConditionNode> { c1, c2 });

        // Act
        var result = await _evaluator.Evaluate(andNode, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_OrNode_ShouldReturnSuccess_WhenOneMatches()
    {
        // Arrange
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"WRONG\"").RootElement);
        var c2 = new ConditionNode("RequiredPermission", Operator.StringEquals, JsonDocument.Parse("\"courses:View\"").RootElement);
        var orNode = new OrNode(new List<IConditionNode> { c1, c2 });

        // Act
        var result = await _evaluator.Evaluate(orNode, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_NotNode_ShouldReturnSuccess_WhenConditionFails()
    {
        // Arrange
        var c1 = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"WRONG\"").RootElement);
        var notNode = new NotNode(c1);

        // Act
        var result = await _evaluator.Evaluate(notNode, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_VariableReference_ShouldReturnSuccess_WhenMatch()
    {
        // Arrange
        // We compare ResourcePath with itself using a variable reference
        var condition = new ConditionNode("ResourcePath", Operator.StringEquals, JsonDocument.Parse("\"$ResourcePath\"").RootElement);

        // Act
        var result = await _evaluator.Evaluate(condition, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }
    
    [Fact]
    public async Task Evaluate_InOperator_ShouldReturnSuccess_WhenValueInArray()
    {
        // Arrange
        var condition = new ConditionNode("ResourcePath", Operator.In, JsonDocument.Parse("[\"course/100\", \"course/101\", \"course/102\"]").RootElement);

        // Act
        var result = await _evaluator.Evaluate(condition, _context);

        // Assert
        result.IsError.Should().BeFalse();
    }
}
