using AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.EnsureCoursePlanPrincipal;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

/// <summary>
/// Step 9 (§3.2.1): windowed plans delegate the per-(course, plan) principal minting to Identity
/// (IIdentityModule.Send → EnsureCoursePlanPrincipalCommand); unlimited plans keep the
/// caller-provided principal (unchanged behavior). No inline policies involved (§5 rule 1).
/// </summary>
public class AddPlanCommandHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly IIdentityModule _identityModule;
    private readonly ILogger<AddPlanCommandHandler> _logger;
    private readonly Course _freshCourse;

    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid CallerPrincipalId = Guid.NewGuid();

    public AddPlanCommandHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _identityModule = Substitute.For<IIdentityModule>();
        _logger = Substitute.For<ILogger<AddPlanCommandHandler>>();

        _freshCourse = Course.Create(CourseId, Guid.NewGuid(), "Title", null, Guid.NewGuid()).Value;
        var builder = Substitute.For<ICourseQueryBuilder>();
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(_freshCourse);
        _courseRepository.Query(CourseId).Returns(builder);
    }

    [Fact]
    public async Task Handle_Should_MintPerPlanPrincipal_And_StoreItOnThePlan_WhenWindowed()
    {
        // Arrange
        var mintedId = Guid.NewGuid();
        _identityModule.Send(Arg.Is<EnsureCoursePlanPrincipalCommand>(c => c.CourseId == CourseId), Arg.Any<CancellationToken>())
            .Returns(mintedId);

        // Act
        var result = await new AddPlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new AddPlanCommand(CourseId, "Windowed", PrincipalId: null, AccessWindowDays: 30), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();

        await _identityModule.Received(1).Send(
            Arg.Is<EnsureCoursePlanPrincipalCommand>(c => c.CourseId == CourseId && c.PlanId == result.Value),
            Arg.Any<CancellationToken>());

        _freshCourse.Plans.Should().ContainSingle();
        var plan = _freshCourse.Plans.Single();
        plan.PrincipalId.Should().Be(mintedId);
        plan.AccessWindowDays.Should().Be(30);
        _courseRepository.Received(1).Update(_freshCourse);
    }

    [Fact]
    public async Task Handle_Should_NotCallIdentity_AndKeepCallerPrincipal_WhenUnlimited()
    {
        // Act
        var result = await new AddPlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new AddPlanCommand(CourseId, "Unlimited", PrincipalId: CallerPrincipalId), CancellationToken.None);

        // Assert — unlimited branch unchanged (no Identity minting)
        result.IsError.Should().BeFalse();
        await _identityModule.DidNotReceive().Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>());

        _freshCourse.Plans.Should().ContainSingle();
        var plan = _freshCourse.Plans.Single();
        plan.PrincipalId.Should().Be(CallerPrincipalId);
        plan.AccessWindowDays.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnError_AndNotAddPlan_WhenMintFails()
    {
        // Arrange — a windowed plan without its principal is invalid (Identity is the authority)
        _identityModule.Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>())
            .Returns(Error.Unexpected("Principal.MintFailed", "boom"));

        // Act
        var result = await new AddPlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new AddPlanCommand(CourseId, "Windowed", PrincipalId: null, AccessWindowDays: 30), CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Principal.MintFailed");
        _freshCourse.Plans.Should().BeEmpty();
        _courseRepository.DidNotReceive().Update(Arg.Any<Course>());
    }

    [Fact]
    public async Task Handle_Should_IgnoreCallerPrincipal_WhenWindowed()
    {
        // Arrange — a stale client that still supplies PrincipalId on a windowed plan
        var mintedId = Guid.NewGuid();
        _identityModule.Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>())
            .Returns(mintedId);

        // Act
        var result = await new AddPlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new AddPlanCommand(CourseId, "Windowed", PrincipalId: CallerPrincipalId, AccessWindowDays: 30), CancellationToken.None);

        // Assert — the minted principal wins (system-managed, not caller-managed)
        result.IsError.Should().BeFalse();
        _freshCourse.Plans.Single().PrincipalId.Should().Be(mintedId);
    }
}
