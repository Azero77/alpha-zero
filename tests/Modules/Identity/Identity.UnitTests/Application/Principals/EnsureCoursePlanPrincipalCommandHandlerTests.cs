using AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Identity.UnitTests.Application.Principals;

/// <summary>
/// Step 9 (§3.2.1): the per-(tenant, course, plan) role principal minted by Identity, carrying
/// the managed policy (courses:View/Read + AccessTicketValid condition, no inline policies — §5.1).
/// </summary>
public class EnsureCoursePlanPrincipalCommandHandlerTests
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<EnsureCoursePlanPrincipalCommandHandler> _logger;
    private readonly EnsureCoursePlanPrincipalCommandHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    public EnsureCoursePlanPrincipalCommandHandlerTests()
    {
        _principalRepository = Substitute.For<IPrincipalRepository>();
        _managedPolicyRepository = Substitute.For<IManagedPolicyRepository>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed");
        _logger = Substitute.For<ILogger<EnsureCoursePlanPrincipalCommandHandler>>();

        _handler = new EnsureCoursePlanPrincipalCommandHandler(
            _principalRepository,
            _managedPolicyRepository,
            _tenantProvider,
            _passwordHasher,
            _logger);
    }

    [Fact]
    public async Task Handle_Should_CreateRolePrincipal_WithManagedPolicyAndAccessTicketValidCondition()
    {
        // Arrange
        _tenantProvider.GetTenant().Returns(TenantId);
        _principalRepository.GetById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Principal?)null);
        _managedPolicyRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy?)null);

        var facts = new CoursePlanPrincipalFacts(TenantId, CourseId, PlanId);

        var principal = (Principal?)null;
        var policy = (AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy?)null;
        _principalRepository.When(r => r.Add(Arg.Any<Principal>())).Do(ci => principal = ci.Arg<Principal>());
        _managedPolicyRepository.When(r => r.Add(Arg.Any<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy>()))
            .Do(ci => policy = ci.Arg<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy>());

        // Act
        var result = await _handler.Handle(new EnsureCoursePlanPrincipalCommand(CourseId, PlanId), CancellationToken.None);

        // Assert — deterministic identity (idempotency contract)
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(facts.PrincipalId);

        principal.Should().NotBeNull();
        principal!.Id.Should().Be(facts.PrincipalId);
        principal.Username.Should().Be(facts.Username);
        principal.PrincipalType.Should().Be(PrincipalType.Role);
        principal.IsManaged.Should().BeTrue(); // null scope — a role principal, no login
        principal.TenantId.Should().Be(TenantId);
        principal.Policies.Should().Contain(p => p is AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy);

        policy.Should().NotBeNull();
        policy!.Name.Should().Be(facts.PolicyName);
        policy.Id.Should().Be(facts.PolicyId);

        var statement = policy.Statements.Should().ContainSingle().Subject;
        statement.Effect.Should().BeTrue();
        statement.Actions.Should().Contain(new[] { "courses:View", "courses:Read" });
        var condition = statement.Condition.Should().BeOfType<ConditionNode>().Subject;
        condition.Operator.Should().Be(Operator.AccessTicketValid);
        condition.Property.Should().Be("Id");
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenPrincipalAlreadyExistsWithPolicy()
    {
        // Arrange — fresh principals per test (static aliases removed)
        _tenantProvider.GetTenant().Returns(TenantId);

        var freshCourse = Guid.NewGuid();
        var freshPlan = Guid.NewGuid();
        var facts = new CoursePlanPrincipalFacts(TenantId, freshCourse, freshPlan);

        var policy = new AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy(
            facts.PolicyId, facts.PolicyName, new());
        var existingPrincipal = Principal.Create(facts.PrincipalId, facts.Username, "hash", facts.DisplayName, PrincipalType.Role, null, TenantId).Value;
        existingPrincipal.AddPolicy(policy);

        _principalRepository.GetById(facts.PrincipalId, Arg.Any<CancellationToken>()).Returns(existingPrincipal);
        _managedPolicyRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(policy);

        // Act — same (course, plan) as a retry after a partial Courses-side failure
        var result = await _handler.Handle(new EnsureCoursePlanPrincipalCommand(freshCourse, freshPlan), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(facts.PrincipalId);
        _principalRepository.DidNotReceive().Add(Arg.Any<Principal>());
        _principalRepository.DidNotReceive().Update(Arg.Any<Principal>());
        _managedPolicyRepository.DidNotReceive().Add(Arg.Any<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy>());
    }

    [Fact]
    public async Task Handle_Should_ReattachPolicy_WhenPrincipalExistsWithoutIt()
    {
        // Arrange — partial state from an earlier run: principal exists, policy missing
        _tenantProvider.GetTenant().Returns(TenantId);

        var freshCourse = Guid.NewGuid();
        var freshPlan = Guid.NewGuid();
        var facts = new CoursePlanPrincipalFacts(TenantId, freshCourse, freshPlan);

        var existingPrincipal = Principal.Create(facts.PrincipalId, facts.Username, "hash", facts.DisplayName, PrincipalType.Role, null, TenantId).Value;
        _principalRepository.GetById(facts.PrincipalId, Arg.Any<CancellationToken>()).Returns(existingPrincipal);
        _managedPolicyRepository.GetFirst(Arg.Any<System.Linq.Expressions.Expression<Func<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy?)null);

        var policy = (AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy?)null;
        _managedPolicyRepository.When(r => r.Add(Arg.Any<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy>()))
            .Do(ci => policy = ci.Arg<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy>());

        // Act
        var result = await _handler.Handle(new EnsureCoursePlanPrincipalCommand(freshCourse, freshPlan), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(facts.PrincipalId);
        _principalRepository.DidNotReceive().Add(Arg.Any<Principal>()); // principal never re-created
        _principalRepository.Received(1).Update(Arg.Any<Principal>()); // the missing policy re-attached
        policy.Should().NotBeNull();
        policy!.Id.Should().Be(facts.PolicyId);
    }

    [Fact]
    public async Task Handle_Should_ReturnTenantNotFound_WhenTenantMissing()
    {
        _tenantProvider.GetTenant().Returns((Guid?)null);

        var result = await _handler.Handle(new EnsureCoursePlanPrincipalCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tenant.NotFound");
        _principalRepository.DidNotReceive().Add(Arg.Any<Principal>());
        _managedPolicyRepository.DidNotReceive().Add(Arg.Any<AlphaZero.Modules.Identity.Domain.Models.Principals.Policies.ManagedPolicy>());
    }
}
