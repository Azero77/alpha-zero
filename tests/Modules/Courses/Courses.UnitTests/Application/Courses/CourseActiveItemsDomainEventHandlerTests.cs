using AlphaZero.Modules.Courses.Application.Courses.EventHandlers;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class CourseActiveItemsDomainEventHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ICourseRepository _courseRepository = Substitute.For<ICourseRepository>();
    private readonly ILogger<CourseActiveItemsDomainEventHandler> _logger = Substitute.For<ILogger<CourseActiveItemsDomainEventHandler>>();
    private readonly CourseActiveItemsDomainEventHandler _handler;

    public CourseActiveItemsDomainEventHandlerTests()
    {
        _handler = new CourseActiveItemsDomainEventHandler(_courseRepository, _logger);
    }

    private static Course CreateCourse()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Masks 101", null, Guid.NewGuid()).Value;
        course.AddSection("S1");
        return course;
    }

    [Fact]
    public async Task Add_Should_ActivateTheAllocatedPosition()
    {
        var course = CreateCourse();
        var section = course.Sections.First();
        var item = course.AddItem(section.Id, "L1", ResourceType.Video).Value;
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        var @event = course.DomainEvents.OfType<CurriculumItemAddedDomainEvent>().Single();

        await _handler.Handle(@event, CancellationToken.None);

        course.ActiveItemsBitmask.Length.Should().Be(1);
        course.ActiveItemsBitmask[0].Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Should_DeactivateWithoutShiftingOtherPositions()
    {
        var course = CreateCourse();
        var section = course.Sections.First();
        var third = course.AddItem(section.Id, "L3", ResourceType.Video).Value;
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        // Burned middle positions: deactivate 0 and 1 in one logical operation
        course.ActivateCurriculumBit(0);
        course.ActivateCurriculumBit(1);
        course.ActivateCurriculumBit(2);

        course.DeactivateCurriculumBits(new[] { 0, 1 });
        await _handler.Handle(new CurriculumItemDeletedDomainEvent(course.Id, section.Id, third.Id, 2), CancellationToken.None);

        course.ActiveItemsBitmask.Length.Should().Be(3); // gaps preserved, no compaction
        course.ActiveItemsBitmask.Cast<bool>().Should().BeEquivalentTo(new[] { false, false, false });
    }

    [Fact]
    public async Task Add_Should_GrowTheMask_PreservingDeactivatedGaps()
    {
        var course = CreateCourse();
        var section = course.Sections.First();
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        course.ActivateCurriculumBit(0);
        course.ActiveItemsBitmask.Length.Should().Be(1);
        course.ActivateCurriculumBit(2); // position 1 stays a deactivated gap

        course.ActiveItemsBitmask.Length.Should().Be(3);
        course.ActiveItemsBitmask[0].Should().BeTrue();
        course.ActiveItemsBitmask[1].Should().BeFalse();
        course.ActiveItemsBitmask[2].Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_Should_Throw_WhenBitIndexIsOutsideTheAllocatedRange()
    {
        var course = CreateCourse();
        var section = course.Sections.First();
        course.AddItem(section.Id, "L1", ResourceType.Video);
        _courseRepository.GetById(course.Id, Arg.Any<CancellationToken>()).Returns(course);

        // Position 5 was never allocated — the handler would treat this as an invariant
        // violation and roll the transaction back, never silently normalize data.
        var act = async () => await _handler.Handle(
            new CurriculumItemDeletedDomainEvent(course.Id, section.Id, Guid.NewGuid(), bitIndex: 5),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SectionAdded_Should_BeANoOp()
    {
        var course = CreateCourse();
        var section = course.Sections.First();

        var act = async () => await _handler.Handle(
            new CourseSectionAddedDomainEvent(course.Id, section.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
        course.ActiveItemsBitmask.Length.Should().Be(0);
    }

    [Fact]
    public async Task MissingCourse_Should_Throw_SoTheTransactionRollsBack()
    {
        _courseRepository.GetById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Course?)null);

        var act = async () => await _handler.Handle(
            new CurriculumItemAddedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
