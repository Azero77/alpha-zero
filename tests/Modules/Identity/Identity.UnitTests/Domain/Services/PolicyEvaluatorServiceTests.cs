using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using FluentAssertions;
using NSubstitute;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Services;

public class PolicyEvaluatorServiceTests
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantUserPrincpialAssignmentRepository _assignmentRepository;
    private readonly IRepository<TenantUser> _userRepository;
    private readonly PolicyEvaluatorService _evaluator;
    
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    public PolicyEvaluatorServiceTests()
    {
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _assignmentRepository = Substitute.For<ITenantUserPrincpialAssignmentRepository>();
        _userRepository = Substitute.For<IRepository<TenantUser>>();

        var strategies = new List<IAuthorizationStrategy>
        {
            new TenantUserAuthorizationStrategy(_assignmentRepository, _userRepository),
            new PrincipalUserAuthorizationStrategy(_principalRepository)
        };

        _evaluator = new PolicyEvaluatorService(strategies);
    }

    [Fact]
    public async Task Authorize_TenantUser_Should_Succeed_WhenValidAssignmentExists()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, "sub-1", "Ali").Value;
        // Correctly set the active session via Reflection if setter is private
        user.GetType().GetProperty(nameof(TenantUser.ActiveSessionId))!
            .SetValue(user, SessionId);
        
        _userRepository.GetById(user.Id).Returns(Task.FromResult<TenantUser?>(user));

        // Use the proper way to create a template since constructor is protected
        var template = new PrincipalTemplate(Guid.NewGuid(), "Student", PrincipalType.Role);
        
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "View", new() 
        { 
            new PolicyTemplateStatement("S1", new() { "courses:View" }, true) 
        });
        
        // Add policy to the template
        template.ManagedPolicies.Add(managedPolicy);

        var assignment = TenantUserPrinciaplAssignment.Create(TenantId, user, template, $"az:courses:{TenantId}:course/101").Value;
        
        // FIX: Must return Task.FromResult for async methods
        _assignmentRepository.Get(user.Id, Arg.Any<string>())
            .Returns(Task.FromResult<TenantUserPrinciaplAssignment?>(assignment));

        // Act
        var result = await _evaluator.Authorize(
            user.Id, 
            TenantId, 
            "course/101", 
            ResourceType.Courses, 
            "courses:View", 
            AuthorizationMethod.TenantUser.ToString(), 
            SessionId);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Authorize_TenantUser_Should_Fail_WhenSessionIdMismatches()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, "sub-1", "Ali").Value;
        _userRepository.GetById(user.Id).Returns(Task.FromResult<TenantUser?>(user));

        // Act
        var result = await _evaluator.Authorize(
            user.Id, 
            TenantId, 
            "path", 
            ResourceType.Courses, 
            "perm", 
            AuthorizationMethod.TenantUser.ToString(), 
            Guid.NewGuid()); // Wrong Session

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Authorize_Principal_Should_EvaluateInlinePolicies()
    {
        // Arrange
        var principalResult = Principal.Create(Guid.NewGuid(), "sub-1", PrincipalType.User, TenantId, ResourcePattern.All.Value, "Custom");
        var principal = principalResult.Value;
        
        var policy = new Policy(Guid.NewGuid(), "Inline", TenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "video:Stream" }, true, new() { ResourcePattern.All }));
        principal.AddInlinePolicy(policy);

        _principalRepository.GetById(principal.Id).Returns(Task.FromResult<Principal?>(principal));

        // Act
        var result = await _evaluator.Authorize(
            principal.Id, 
            TenantId, 
            "video/1", 
            ResourceType.Videos, 
            "video:Stream", 
            AuthorizationMethod.Principal.ToString());

        // Assert
        result.IsError.Should().BeFalse();
    }
}
