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
/// Step 9 (§3.2.1 / §3.2.5): plan edits are future-only. Windowed plans own a system-minted
/// per-(course, plan) principal (minted on the first windowed update, kept across window edits);
/// unlimited plans keep the caller-controlled principal.
/// </summary>
public class UpdatePlanCommandHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly IIdentityModule _identityModule;
    private readonly ILogger<UpdatePlanCommandHandler> _logger;
    private readonly Course _course;

    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly Guid ExistingPrincipalId = Guid.NewGuid();

    public UpdatePlanCommandHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _identityModule = Substitute.For<IIdentityModule>();
        _logger = Substitute.For<ILogger<UpdatePlanCommandHandler>>();

        _course = Course.Create(CourseId, Guid.NewGuid(), "Title", null, Guid.NewGuid()).Value;
        _course.AddPlan("Legacy", ExistingPrincipalId, planId: PlanId).Value.Should().NotBeNull();

        var builder = Substitute.For<ICourseQueryBuilder>();
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(_course);
        _courseRepository.Query(CourseId).Returns(builder);
    }

    [Fact]
    public async Task Handle_Should_MintPrincipal_WhenLegacyPlanGainsWindowWithoutOne()
    {
        // Arrange — a legacy unlimited plan (principal set) becomes windowed empty-principal?
        // This test uses a plan WITHOUT a principal: simulate the pre-step-9 impossible case
        // where the stored principal id is empty.
        var emptyPrincipalCourse = Course.Create(CourseId, Guid.NewGuid(), "T", null, Guid.NewGuid()).Value;
        emptyPrincipalCourse.AddPlan("Windowed", Guid.Empty, planId: PlanId).Value.Should().NotBeNull();
        SubstituteCourseQuery(emptyPrincipalCourse);

        var mintedId = Guid.NewGuid();
        _identityModule.Send(Arg.Is<EnsureCoursePlanPrincipalCommand>(c => c.CourseId == CourseId && c.PlanId == PlanId), Arg.Any<CancellationToken>())
            .Returns(mintedId);

        // Act — windowed update on a plan with no principal
        var result = await new UpdatePlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new UpdatePlanCommand(CourseId, PlanId, "Windowed", PrincipalId: null, AccessWindowDays: 14), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _identityModule.Received(1).Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>());
        emptyPrincipalCourse.Plans.Single().PrincipalId.Should().Be(mintedId);
        emptyPrincipalCourse.Plans.Single().AccessWindowDays.Should().Be(14);
    }

    [Fact]
    public async Task Handle_Should_KeepSystemOwnedPrincipal_WhenEditingWindowOfWindowedPlan()
    {
        // Arrange — already-windowed plan (its principal stands); window days change, principal stays
        var windowedCourse = Course.Create(CourseId, Guid.NewGuid(), "T", null, Guid.NewGuid()).Value;
        windowedCourse.AddPlan("Windowed", ExistingPrincipalId, planId: PlanId, accessWindowDays: 30).Value.Should().NotBeNull();
        SubstituteCourseQuery(windowedCourse);

        // Act
        var result = await new UpdatePlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new UpdatePlanCommand(CourseId, PlanId, "Windowed", PrincipalId: null, AccessWindowDays: 60), CancellationToken.None);

        // Assert — "they bought 30 days" edits are future-only, principal untouched, no minting
        result.IsError.Should().BeFalse();
        await _identityModule.DidNotReceive().Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>());
        windowedCourse.Plans.Single().PrincipalId.Should().Be(ExistingPrincipalId);
        windowedCourse.Plans.Single().AccessWindowDays.Should().Be(60);
    }

    [Fact]
    public async Task Handle_Should_KeepExistingPrincipal_WhenUnlimitedUpdateOmitsPrincipal()
    {
        // Act — unlimited update with no caller principal (kept — legacy behavior safe default)
        var result = await new UpdatePlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new UpdatePlanCommand(CourseId, PlanId, "Standard", PrincipalId: null), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _identityModule.DidNotReceive().Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>());
        _course.Plans.Single().PrincipalId.Should().Be(ExistingPrincipalId);
        _course.Plans.Single().AccessWindowDays.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_HonorCallerPrincipal_WhenUnlimitedUpdateSuppliesOne()
    {
        // Arrange — admins can still swap an unlimited plan's principal (pre-existing behavior)
        var newPrincipalId = Guid.NewGuid();

        // Act
        var result = await new UpdatePlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new UpdatePlanCommand(CourseId, PlanId, "Standard", PrincipalId: newPrincipalId), CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        await _identityModule.DidNotReceive().Send(Arg.Any<EnsureCoursePlanPrincipalCommand>(), Arg.Any<CancellationToken>());
        _course.Plans.Single().PrincipalId.Should().Be(newPrincipalId);
    }

    [Fact]
    public async Task Handle_Should_ReturnPlanNotFound_WhenPlanMissing()
    {
        var result = await new UpdatePlanCommandHandler(_courseRepository, _identityModule, _logger)
            .Handle(new UpdatePlanCommand(CourseId, Guid.NewGuid(), "X", PrincipalId: null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Plan");
    }

    private void SubstituteCourseQuery(Course course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
        _courseRepository.Query(CourseId).Returns(builder);
    }
}
