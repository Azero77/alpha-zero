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

    public PolicyEvaluatorServiceTests()
    {
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _assignmentRepository = Substitute.For<ITenantUserPrincpialAssignmentRepository>();
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        var conditionRepository = Substitute.For<IConditionRepository>();
        var evaluationEngine = new PolicyEvaluationEngine(conditionRepository);

        var strategies = new List<IAuthorizationStrategy>
        {
            new TenantUserAuthorizationStrategy(_assignmentRepository, evaluationEngine),
            new PrincipalUserAuthorizationStrategy(_principalRepository, evaluationEngine)
        };

        _evaluator = new PolicyEvaluatorService(strategies);
    }

    [Fact]
    public async Task Authorize_TenantUser_Should_Succeed_WhenValidAssignmentExists()
    {
        // Arrange
        var user = TenantUser.Create(TenantId, "sub-1", "Ali", TenantUserDeviceInfo.Empty).Value;
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
        var result = await _evaluator.Authorize(new AuthorizationContext()
        {
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
            Id = user.Id,
            TenantId = TenantId,
            RequiredPermission = "courses:View",
            ResourcePath = "course/101",
            ResourceType = ResourceType.Courses
        });
        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Authorize_Principal_Should_EvaluateInlinePolicies()
    {
        // Arrange
        var principalResult = Principal.Create(Guid.NewGuid(), "iam-user-1", PrincipalType.User, TenantId, ResourcePattern.All.Value, "Custom", "hashed-password");
        var principal = principalResult.Value;
        
        var policy = new Policy(Guid.NewGuid(), "Inline", TenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "video:Stream" }, true, new() { ResourcePattern.All }));
        principal.AddInlinePolicy(policy);

        _principalRepository.GetById(principal.Id).Returns(Task.FromResult<Principal?>(principal));
        var context = new AuthorizationContext()
        {
            AuthenticationMethod = AuthenticationMethod.Principal.ToString(),
            Id = principal.Id,
            TenantId = TenantId,
            RequiredPermission = "video:Stream",
            ResourcePath = "video/123",
            ResourceType = ResourceType.Video
        };
        // Act
        var result = await _evaluator.Authorize(
            context
            );

        // Assert
        result.IsError.Should().BeFalse();
    }
}
