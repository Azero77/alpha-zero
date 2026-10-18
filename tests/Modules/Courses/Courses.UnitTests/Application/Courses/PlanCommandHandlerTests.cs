using AlphaZero.Modules.Courses.Application.Courses.Commands.Plans;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

/// <summary>
/// Plan command handler coverage: course-missing errors, delegation to the domain
/// AddPlan/UpdatePlan/RemovePlan semantics (error passthrough) and repository updates.
/// </summary>
public class PlanCommandHandlerTests
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<AddPlanCommandHandler> _addLogger;
    private readonly ILogger<UpdatePlanCommandHandler> _updateLogger;
    private readonly ILogger<RemovePlanCommandHandler> _removeLogger;
    private readonly AddPlanCommandHandler _addHandler;
    private readonly UpdatePlanCommandHandler _updateHandler;
    private readonly RemovePlanCommandHandler _removeHandler;

    public PlanCommandHandlerTests()
    {
        _courseRepository = Substitute.For<ICourseRepository>();
        _addLogger = Substitute.For<ILogger<AddPlanCommandHandler>>();
        _updateLogger = Substitute.For<ILogger<UpdatePlanCommandHandler>>();
        _removeLogger = Substitute.For<ILogger<RemovePlanCommandHandler>>();
        _addHandler = new AddPlanCommandHandler(_courseRepository, _addLogger);
        _updateHandler = new UpdatePlanCommandHandler(_courseRepository, _updateLogger);
        _removeHandler = new RemovePlanCommandHandler(_courseRepository, _removeLogger);
    }

    private void SetupQuery(Guid courseId, Course? course)
    {
        var builder = Substitute.For<ICourseQueryBuilder>();
        _courseRepository.Query(courseId).Returns(builder);
        builder.WithPlans(Arg.Any<bool>()).Returns(builder);
        builder.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(course);
    }

    private static Course NewCourse(Guid tenantId)
        => Course.Create(Guid.NewGuid(), tenantId, "Title", "Desc", Guid.NewGuid()).Value;

    // ---- AddPlan ----

    [Fact]
    public async Task AddPlan_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        SetupQuery(courseId, null);
        var command = new AddPlanCommand(courseId, "Standard", Guid.NewGuid());

        // Act
        var result = await _addHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
        _courseRepository.DidNotReceiveWithAnyArgs().Update(default!);
    }

    [Fact]
    public async Task AddPlan_Should_AddPlan_AndUpdateCourse_WhenNameIsNew()
    {
        // Arrange
        var course = NewCourse(Guid.NewGuid());
        SetupQuery(course.Id, course);
        var principalId = Guid.NewGuid();
        var command = new AddPlanCommand(course.Id, "Standard", principalId);

        // Act
        var result = await _addHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var plan = course.Plans.Should().ContainSingle().Subject;
        result.Value.Should().Be(plan.Id);
        plan.Name.Should().Be("Standard");
        plan.PrincipalId.Should().Be(principalId);
        _courseRepository.Received(1).Update(course);
    }

    [Fact]
    public async Task AddPlan_Should_ReturnConflict_WhenNameAlreadyExists()
    {
        // Arrange
        var course = NewCourse(Guid.NewGuid());
        course.AddPlan("Standard", Guid.NewGuid());
        SetupQuery(course.Id, course);
        var command = new AddPlanCommand(course.Id, "standard", Guid.NewGuid());

        // Act
        var result = await _addHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.PlanExists");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        course.Plans.Should().ContainSingle("the duplicate plan must not be added");
        _courseRepository.DidNotReceiveWithAnyArgs().Update(default!);
    }

    // ---- UpdatePlan ----

    [Fact]
    public async Task UpdatePlan_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        SetupQuery(courseId, null);
        var command = new UpdatePlanCommand(courseId, Guid.NewGuid(), "Renamed", Guid.NewGuid());

        // Act
        var result = await _updateHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
        _courseRepository.DidNotReceiveWithAnyArgs().Update(default!);
    }

    [Fact]
    public async Task UpdatePlan_Should_Fail_WhenPlanDoesNotExist()
    {
        // Arrange
        var course = NewCourse(Guid.NewGuid());
        SetupQuery(course.Id, course);
        var command = new UpdatePlanCommand(course.Id, Guid.NewGuid(), "Renamed", Guid.NewGuid());

        // Act
        var result = await _updateHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Plan");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdatePlan_Should_Rename_WhenPlanExists()
    {
        // Arrange
        var course = NewCourse(Guid.NewGuid());
        var plan = course.AddPlan("Standard", Guid.NewGuid()).Value;
        SetupQuery(course.Id, course);
        var principalId = Guid.NewGuid();
        var command = new UpdatePlanCommand(course.Id, plan.Id, "Premium", principalId);

        // Act
        var result = await _updateHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        plan.Name.Should().Be("Premium");
        plan.PrincipalId.Should().Be(principalId);
        course.Plans.Should().ContainSingle();
    }

    // ---- RemovePlan ----

    [Fact]
    public async Task RemovePlan_Should_Fail_WhenCourseDoesNotExist()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        SetupQuery(courseId, null);
        var command = new RemovePlanCommand(courseId, Guid.NewGuid());

        // Act
        var result = await _removeHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotFound");
        _courseRepository.DidNotReceiveWithAnyArgs().Update(default!);
    }

    [Fact]
    public async Task RemovePlan_Should_Fail_WhenPlanDoesNotExist()
    {
        // Arrange
        var course = NewCourse(Guid.NewGuid());
        SetupQuery(course.Id, course);
        var command = new RemovePlanCommand(course.Id, Guid.NewGuid());

        // Act
        var result = await _removeHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Plan");
        course.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovePlan_Should_RemoveExistingPlan()
    {
        // Arrange
        var course = NewCourse(Guid.NewGuid());
        var plan = course.AddPlan("Standard", Guid.NewGuid()).Value;
        SetupQuery(course.Id, course);
        var command = new RemovePlanCommand(course.Id, plan.Id);

        // Act
        var result = await _removeHandler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        course.Plans.Should().BeEmpty();
    }
}
