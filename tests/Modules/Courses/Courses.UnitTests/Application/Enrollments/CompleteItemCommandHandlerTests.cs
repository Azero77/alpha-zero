using AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Aggregates.Enrollments;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CompleteItemCommandHandler> _logger = Substitute.For<ILogger<CompleteItemCommandHandler>>();
    private readonly CompleteItemCommandHandler _handler;

    public CompleteItemCommandHandlerTests()
    {
        _handler = new CompleteItemCommandHandler(_enrollmentRepository, _courseRepository, _unitOfWork, _logger);
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

    // --- Optimistic-concurrency retry (#32/D3) ---

    [Fact]
    public async Task Handle_Should_RetryOnce_AgainstFreshState_WhenSaveConflicts()
    {
        // Arrange: a course with two active items; a concurrent writer already committed bit 0
        // so the handler's stale save loses the rowversion race.
        var course = CreateCourseWithActiveBits(2, 0, 1);
        var staleEnrollment = Enrollment.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id).Value;
        _enrollmentRepository.GetByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>()).Returns(staleEnrollment);
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);
        _enrollmentRepository.GetFreshByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>())
            .Returns(staleEnrollment); // the "fresh" reload reflects the other writer's commit

        var saveCalls = 0;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (saveCalls++ == 0) throw new DbUpdateConcurrencyException("row version conflict");
                return 1;
            });

        // Act: complete bit 0 — the fresh state already carries it (completed by the other writer)
        var result = await _handler.Handle(new CompleteItemCommand(staleEnrollment.Id, 0), CancellationToken.None);

        // Assert: retried exactly once, save runs twice, and the fresh row ends up correct
        result.IsError.Should().BeFalse();
        saveCalls.Should().Be(2);
        _ = _enrollmentRepository.Received(1).GetFreshByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>());
        staleEnrollment.Progress.IsComplete(0).Should().BeTrue();
        staleEnrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty(
            because: "one of two active items is still incomplete");
    }

    [Fact]
    public async Task Handle_Retry_Should_SuppressDuplicateCompletionEvent_WhenFreshRowAlreadyCompleted()
    {
        // Arrange: BOTH active items are completed by the concurrent winner; the loser's
        // stale save conflicts, and the fresh reload already carries CompletedOn.
        var course = CreateCourseWithActiveBits(2, 0, 1);
        var staleEnrollment = Enrollment.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id).Value;
        staleEnrollment.CompleteItem(0, course.SnapshotActiveItemsBitmask());
        staleEnrollment.PopDomainEvents();
        _enrollmentRepository.GetByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>()).Returns(staleEnrollment);
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        // The fresh row = the concurrent winner's state: same aggregate content, CompletedOn stamped
        var freshEnrollment = Enrollment.Create(staleEnrollment.Id, TenantId, staleEnrollment.StudentId, course.Id).Value;
        freshEnrollment.CompleteItem(0, course.SnapshotActiveItemsBitmask());
        freshEnrollment.CompleteItem(1, course.SnapshotActiveItemsBitmask());
        freshEnrollment.PopDomainEvents();
        freshEnrollment.CompletedOn.Should().NotBeNull("setup: the concurrent winner completed the course");
        _enrollmentRepository.GetFreshByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>()).Returns(freshEnrollment);

        var saveCalls = 0;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (saveCalls++ == 0) throw new DbUpdateConcurrencyException("row version conflict");
                return 1;
            });

        // Act: the loser tries to complete the same last item
        var result = await _handler.Handle(new CompleteItemCommand(staleEnrollment.Id, 1), CancellationToken.None);

        // Assert: retry succeeds idempotently; NO duplicate completion milestone is raised
        result.IsError.Should().BeFalse();
        saveCalls.Should().Be(2);
        freshEnrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().BeEmpty(
            because: "the CompletedOn guard suppresses the duplicate completion event on retry");
        freshEnrollment.CompletedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Retry_Should_KeepLostBit_NoLostUpdate_WhenConcurrentBitsDiffer()
    {
        // Arrange: two writers complete DIFFERENT last bits concurrently (2-item course).
        var course = CreateCourseWithActiveBits(2, 0, 1);
        var staleEnrollment = Enrollment.Create(Guid.NewGuid(), TenantId, Guid.NewGuid(), course.Id).Value;
        _enrollmentRepository.GetByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>()).Returns(staleEnrollment);
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        // The fresh row = the winner merged first bit 0; the loser's bit 1 must be re-applied on top.
        var freshEnrollment = Enrollment.Create(staleEnrollment.Id, TenantId, staleEnrollment.StudentId, course.Id).Value;
        freshEnrollment.CompleteItem(0, course.SnapshotActiveItemsBitmask());
        _enrollmentRepository.GetFreshByIdAsync(staleEnrollment.Id, Arg.Any<CancellationToken>()).Returns(freshEnrollment);

        var saveCalls = 0;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (saveCalls++ == 0) throw new DbUpdateConcurrencyException("row version conflict");
                return 1;
            });

        // Act
        var result = await _handler.Handle(new CompleteItemCommand(staleEnrollment.Id, 1), CancellationToken.None);

        // Assert: the merged mask contains BOTH bits, and the completion milestone fires exactly once
        result.IsError.Should().BeFalse();
        saveCalls.Should().Be(2);
        freshEnrollment.Progress.IsComplete(0).Should().BeTrue();
        freshEnrollment.Progress.IsComplete(1).Should().BeTrue();
        freshEnrollment.DomainEvents.OfType<CourseCompletedDomainEvent>().Should().ContainSingle();
    }
}
