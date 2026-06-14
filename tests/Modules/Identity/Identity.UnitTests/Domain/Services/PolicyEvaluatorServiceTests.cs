using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Models.Principals.Policies;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Repositores;
using Microsoft.Extensions.Caching.Hybrid;
using ErrorOr;
using FluentAssertions;
using NSubstitute;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Services;

public class PolicyEvaluatorServiceTests
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly ITenantUserPrincipalAssignmentRepository _assignmentRepository;
    private readonly IRepository<TenantUser> _userRepository;
    private readonly PolicyEvaluatorService _evaluator;
    
    private static readonly Guid TenantId = Guid.NewGuid();

    public PolicyEvaluatorServiceTests()
    {
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _assignmentRepository = Substitute.For<ITenantUserPrincipalAssignmentRepository>();
        _userRepository = Substitute.For<IRepository<TenantUser>>();
        var conditionRepository = Substitute.For<IConditionRepository>();
        var operationEvaluators = Enumerable.Empty<IOperationEvaluator>();
        var conditionEvaluator = new ConditionEvaluatorService(conditionRepository, operationEvaluators);
        var evaluationEngine = new PolicyEvaluationEngine(conditionEvaluator);

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
        var user = TenantUser.Create(TenantId, "sub-1", "Ali").Value;
        _userRepository.GetById(user.Id).Returns(Task.FromResult<TenantUser?>(user));

        var principal = Principal.Create(Guid.NewGuid(), "student-role", "hash", "Student", PrincipalType.Role, null, TenantId).Value;
        
        var managedPolicy = new ManagedPolicy(Guid.NewGuid(), "View", new() 
        { 
            new ManagedPolicyStatement("S1", new() { "courses:View" }, true) 
        });
        
        principal.AddPolicy(managedPolicy);

        var assignment = TenantUserPrincipalAssignment.Create(TenantId, user, principal, $"az:course:{TenantId}:course/101").Value;
        
        _assignmentRepository.GetActiveAssignment(user.Id, Arg.Any<string>())
            .Returns(Task.FromResult(new List<TenantUserPrincipalAssignment> { assignment }));

        // Act
        var result = await _evaluator.Authorize(new AuthorizationContext()
        {
            AuthenticationMethod = AuthenticationMethod.TenantUser.ToString(),
            Id = user.Id,
            TenantId = TenantId,
            RequiredPermission = "courses:View",
            ResourcePath = "course/101",
            ResourceType = "course"
        });
        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Authorize_Principal_Should_EvaluateInlinePolicies()
    {
        // Arrange
        var principal = Principal.Create(Guid.NewGuid(), "iam-user-1", "hashed-password", "Custom", PrincipalType.User, "az:*:*:*", TenantId).Value;
        
        var policy = new InlinePolicy(Guid.NewGuid(), "Inline", TenantId);
        policy.AddStatement(new PolicyStatement("S1", new() { "video:Stream" }, true, new() { ResourcePattern.All }));
        principal.AddPolicy(policy);

        _principalRepository.GetById(principal.Id).Returns(Task.FromResult<Principal?>(principal));
        var context = new AuthorizationContext()
        {
            AuthenticationMethod = AuthenticationMethod.Principal.ToString(),
            Id = principal.Id,
            TenantId = TenantId,
            RequiredPermission = "video:Stream",
            ResourcePath = "video/123",
            ResourceType = "video"
        };
        // Act
        var result = await _evaluator.Authorize(
            context
            );

        // Assert
        result.IsError.Should().BeFalse();
    }
}

public class FakeHybridCache : HybridCache
{
    private readonly Dictionary<string, object> _cache = new();

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options,
        IEnumerable<string>? tags,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return (T)value;
        }

        var newValue = await factory(state, cancellationToken);
        _cache[key] = newValue!;
        return newValue;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options,
        IEnumerable<string>? tags,
        CancellationToken cancellationToken)
    {
        _cache[key] = value!;
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken)
    {
        _cache.Remove(key);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(
        string tag,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
