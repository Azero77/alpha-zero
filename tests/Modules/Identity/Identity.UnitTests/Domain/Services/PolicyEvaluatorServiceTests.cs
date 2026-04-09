using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using ErrorOr;
using FluentAssertions;
using NSubstitute;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Services;

public class PolicyEvaluatorServiceTests
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPrincipalRepository _principalRepository;
    private readonly PolicyEvaluatorService _evaluator;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PrincipalId = Guid.NewGuid();

    public PolicyEvaluatorServiceTests()
    {
        _policyRepository = Substitute.For<IPolicyRepository>();
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _evaluator = new PolicyEvaluatorService(_policyRepository, _principalRepository);
    }

    [Fact]
    public async Task Authorize_Should_ReturnForbidden_WhenNoPoliciesExist()
    {
        // Arrange: No policies set up
        var principal = CreateBasePrincipal();
        _principalRepository.GetById(PrincipalId).Returns(principal);
        _policyRepository.GetManagedPoliciesForPrincipal(PrincipalId).Returns(new List<ManagedPolicy>());

        // Act
        var result = await _evaluator.Authorize(PrincipalId, TenantId, "course/1", ResourceType.Courses, "courses:Edit");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Authorize_Should_ReturnSuccess_WhenInlineAllowExists()
    {
        // Arrange
        var principal = CreateBasePrincipal();
        var policy = new Policy(Guid.NewGuid(), "Inline", TenantId);
        policy.AddStatement(new PolicyStatement("Allow", new() { "courses:Edit" },true, new() { "az:*" }));
        principal.AddInlinePolicy(policy);

        _principalRepository.GetById(PrincipalId).Returns(principal);

        // Act
        var result = await _evaluator.Authorize(PrincipalId, TenantId, "course/1", ResourceType.Courses, "courses:Edit");

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Authorize_Should_ReturnForbidden_WhenExplicitDenyOverridesAllow()
    {
        // Arrange
        var principal = CreateBasePrincipal();
        
        // 1. Allow Policy
        var allowPolicy = new Policy(Guid.NewGuid(), "Allow", TenantId);
        allowPolicy.AddStatement(new PolicyStatement("test Allow", new() { "courses:Delete" }, true, new() { "*" }));
        principal.AddInlinePolicy(allowPolicy);

        // 2. Deny Policy (Same Action)
        var denyPolicy = new Policy(Guid.NewGuid(), "Deny", TenantId);
        denyPolicy.AddStatement(new PolicyStatement("test Deny", new() { "courses:Delete" }, false, new() { "*" }));
        principal.AddInlinePolicy(denyPolicy);

        _principalRepository.GetById(PrincipalId).Returns(principal);

        // Act
        var result = await _evaluator.Authorize(PrincipalId, TenantId, "course/1", ResourceType.Courses, "courses:Delete");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Access.Denied");
    }

    [Fact]
    public async Task Authorize_Should_EnforcePrincipalScopeUrn_ForManagedPolicies()
    {
        // Arrange: Principal is only scoped to Course 123
        var scope = $"az:courses:{TenantId}:course/123/*";
        var principal = Principal.Create(PrincipalId, "identity-1", PrincipalType.User, TenantId, scope, "Teacher").Value;
        
        // Managed Policy allows "Edit" on anything ("*")
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "StandardEditor", new() { new PolicyTemplateStatement("S1", new() { "courses:Edit" }, true) });
        _principalRepository.GetById(PrincipalId).Returns(principal);
        _policyRepository.GetManagedPoliciesForPrincipal(PrincipalId).Returns(new List<ManagedPolicy> { managedPolicy });

        // Act 1: Try to edit Course 123 (Inside Scope)
        var resultAllowed = await _evaluator.Authorize(PrincipalId, TenantId, "course/123", ResourceType.Courses, "courses:Edit");

        // Act 2: Try to edit Course 999 (Outside Scope)
        var resultDenied = await _evaluator.Authorize(PrincipalId, TenantId, "course/999", ResourceType.Courses, "courses:Edit");

        // Assert
        resultAllowed.IsError.Should().BeFalse();
        resultDenied.IsError.Should().BeTrue();
        resultDenied.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    private Principal CreateBasePrincipal()
    {
        return Principal.Create(PrincipalId, "identity-1", PrincipalType.User, TenantId, "az:courses:*:*", "Test User").Value;
    }
}
