using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Events;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class BitmaskStabilityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    /// <summary>
    /// Cursor-level equivalent of what the CourseActiveItemsDomainEventHandler does for
    /// CurriculumItemAddedDomainEvent (unit tests don't run the dispatcher).
    /// </summary>
    private static CurriculumItem AddItem(Course course, CourseSection section, string title, string type)
    {
        var item = course.AddItem(section.Id, title, CurriculumItem.ParseMainType(type)).Value;
        course.ActivateCurriculumBit(item.BitIndex);
        return item;
    }

    [Fact]
    public void BitIndex_Should_BeAssignedSequentially_WhenItemsAreAdded()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("Section 1");
        var section = course.Sections.First();

        // Act
        AddItem(course, section, "Lesson 1", "Video");
        AddItem(course, section, "Assessment 1", "Assessment");
        AddItem(course, section, "Lesson 2", "Video");

        // Assert
        var items = course.Sections.First().Items.ToList();
        items[0].BitIndex.Should().Be(0);
        items[1].BitIndex.Should().Be(1);
        items[2].BitIndex.Should().Be(2);
        course.TotalTrackedItems.Should().Be(3);
    }

    [Fact]
    public void BitIndex_Should_RemainStable_WhenItemsAreReordered()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("Section 1");
        var section = course.Sections.First();

        var l1 = AddItem(course, section, "Lesson 1", "Video");
        var l2 = AddItem(course, section, "Lesson 2", "Video");

        l1.BitIndex.Should().Be(0);
        l2.BitIndex.Should().Be(1);

        // Act: Reorder L2 to be first
        course.ReorderItems(section.Id, new List<Guid> { l2.Id, l1.Id });

        // Assert
        l1.BitIndex.Should().Be(0);
        l1.Order.Should().Be(1);

        l2.BitIndex.Should().Be(1);
        l2.Order.Should().Be(0);
    }

    [Fact]
    public void BitIndex_Should_NotChange_WhenNewSectionsAreAdded()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("S1");
        var s1 = course.Sections.First();

        var l1 = AddItem(course, s1, "L1", "Video");

        l1.BitIndex.Should().Be(0);

        // Act
        course.AddSection("S2");
        var s2 = course.Sections.Last();

        var l2 = AddItem(course, s2, "L2", "Video");

        // Assert
        l1.BitIndex.Should().Be(0);
        l2.BitIndex.Should().Be(1);
    }

    [Fact]
    public void DeletedBitIndexes_Should_NeverBeReused_AndGapsMustBePreserved()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("S1");
        var s1 = course.Sections.First();

        var l1 = AddItem(course, s1, "L1", "Video");
        AddItem(course, s1, "L2", "Video");
        var l3 = AddItem(course, s1, "L3", "Video");

        // Act: burn positions 0 and 1
        course.RemoveItem(s1.Id, l1.Id);
        course.RemoveItem(s1.Id, course.Sections.First().Items.First(i => i.BitIndex == 1).Id);

        // Assert: the next allocation is 3, never a recycled 0/1
        var l4 = AddItem(course, s1, "L4", "Video");
        l4.BitIndex.Should().Be(3);
        course.TotalTrackedItems.Should().Be(4);

        // The surviving item keeps its BitIndex
        l3.BitIndex.Should().Be(2);
    }

    [Fact]
    public void AddItem_Should_RaiseEvent_WithAllocatedBitIndex()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("S1");
        var section = course.Sections.First();

        course.AddItem(section.Id, "L1", ResourceType.Video);

        var @event = course.DomainEvents.OfType<CurriculumItemAddedDomainEvent>().Single();
        @event.CourseId.Should().Be(course.Id);
        @event.SectionId.Should().Be(section.Id);
        @event.BitIndex.Should().Be(0);
        @event.ItemId.Should().Be(course.Sections.First().Items.Single().Id);
    }

    [Fact]
    public void RemoveItem_Should_RaiseEvent_WithBitIndexCapturedBeforeRemoval()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Roadmap 101", null, SubjectId).Value;
        course.AddSection("S1");
        var s1 = course.Sections.First();
        var item = AddItem(course, s1, "L1", "Video");

        course.RemoveItem(s1.Id, item.Id);

        var @event = course.DomainEvents.OfType<CurriculumItemDeletedDomainEvent>().Single();
        @event.CourseId.Should().Be(course.Id);
        @event.SectionId.Should().Be(s1.Id);
        @event.ItemId.Should().Be(item.Id);
        @event.BitIndex.Should().Be(0);
        item.IsDeleted.Should().BeTrue();
        item.BitIndex.Should().Be(0); // index retained by the deleted item
    }

    [Fact]
    public void AddItem_Should_NotBurnAnAllocationSlot_WhenCreationFails()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Empty title 101", null, SubjectId).Value;
        course.AddSection("S1");
        var s1 = course.Sections.First();

        var result = course.AddItem(s1.Id, "", ResourceType.Video);

        result.IsError.Should().BeTrue();
        course.TotalTrackedItems.Should().Be(0); // slot returned — a failed add allocates nothing
    }
}
