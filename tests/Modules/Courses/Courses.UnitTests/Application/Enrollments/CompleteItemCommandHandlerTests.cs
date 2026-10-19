using AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using CourseAggregate = AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Enrollments;

public class CompleteItemCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IEnrollmentRepository _enrollmentRepository = Substitute.For<IEnrollmentRepository>();
    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly ILogger<CompleteItemCommandHandler> _logger = Substitute.For<ILogger<CompleteItemCommandHandler>>();
    private readonly CompleteItemCommandHandler _handler;

    public CompleteItemCommandHandlerTests()
    {
        _handler = new CompleteItemCommandHandler(_enrollmentRepository, _courseRepository, _logger);
    }

    /// <summary>
    /// Seeds a course whose ActiveItemsBitmask reflects the (not-dispatched) mask handler
    /// translation: bits in <paramref name="activeBits"/> are activated for real items.
    /// </summary>
    private static CourseAggregate CreateCourseWithActiveBits(int bits, params int[] activeBits)
    {
        var course = CourseAggregate.Create(Guid.NewGuid(), TenantId, "Fluent 101", null, Guid.NewGuid()).Value;
        course.AddSection("S1");
        var section = course.Sections.First();

        var mask = new System.Collections.BitArray(bits, false);
        foreach (var bit in activeBits) mask[bit] = true;

        for (var i = 0; i < bits; i++)
        {
            var item = course.AddItem(section.Id, $"Item {i}", ResourceType.Video).Value;
            if (mask[item.BitIndex]) course.ActivateCurriculumBit(item.BitIndex);
        }

        return course;
    }

    private void RegisterEnrollmentFor(CourseAggregate course, out Enrollment enrollment)
    {
        enrollment = Enrollment.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id).Value;
        _enrollmentRepository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenEnrollmentIsSuspended()
    {
        // Arrange
        var course = CreateCourseWithActiveBits(5, 0, 1, 2, 3, 4);
        RegisterEnrollmentFor(course, out var enrollment);
        enrollment.Deactivate(); // Status = Inactive

        var command = new CompleteItemCommand(enrollment.Id, 0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Enrollment.Status");
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenEnrollmentIsActive_AndItemExists()
    {
        // Arrange
        var course = CreateCourseWithActiveBits(5, 0, 1, 2, 3, 4);
        RegisterEnrollmentFor(course, out var enrollment);

        var command = new CompleteItemCommand(enrollment.Id, 0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        enrollment.Progress.IsComplete(0).Should().BeTrue();
        enrollment.Progress.Bitmask.Length.Should().Be(1); // lazily grown to the completed position
    }

    [Fact]
    public async Task Handle_Should_RejectCompletingDeactivatedItem()
    {
        // Arrange: position 1 was allocated but got deactivated (item deleted)
        var course = CreateCourseWithActiveBits(3, 0, 2);
        RegisterEnrollmentFor(course, out var enrollment);

        var result = await _handler.Handle(new CompleteItemCommand(enrollment.Id, 1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Progress.ItemInactive");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCourseIsMissing()
    {
        // Arrange
        var course = CreateCourseWithActiveBits(3, 0, 1, 2);
        var enrollment = Enrollment.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id).Value;
        _enrollmentRepository.GetByIdAsync(enrollment.Id, Arg.Any<CancellationToken>()).Returns(enrollment);
        _courseRepository.GetById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CourseAggregate?)null);

        var result = await _handler.Handle(new CompleteItemCommand(enrollment.Id, 0), CancellationToken.None);

        result.FirstError.Code.Should().Be("Course.NotFound");
    }
}
