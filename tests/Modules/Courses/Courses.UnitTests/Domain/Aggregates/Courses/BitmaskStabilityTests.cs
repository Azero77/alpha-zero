using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class BitmaskStabilityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    [Fact]
    public void BitIndex_Should_BeAssignedSequentially_WhenItemsAreAdded()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        // Act
        course.AddCurriculumItem(sectionId, "Lesson 1", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        course.AddCurriculumItem(sectionId, "Assessment 1", "Quiz", ResourceArn.ForAssessment(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        course.AddCurriculumItem(sectionId, "Lesson 2", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));

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
        var sectionId = course.Sections.First().Id;
        course.AddCurriculumItem(sectionId, "Lesson 1", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        course.AddCurriculumItem(sectionId, "Lesson 2", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        
        var l1 = course.Sections.First().Items.First(i => i.Title == "Lesson 1");
        var l2 = course.Sections.First().Items.First(i => i.Title == "Lesson 2");
        l1.BitIndex.Should().Be(0);
        l2.BitIndex.Should().Be(1);

        // Act: Reorder L2 to be first
        course.ReorderItems(sectionId, new List<Guid> { l2.Id, l1.Id });

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
        var s1Id = course.Sections.First().Id;
        course.AddCurriculumItem(s1Id, "L1", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));
        
        var l1 = course.Sections.First().Items.First();
        l1.BitIndex.Should().Be(0);

        // Act
        course.AddSection("S2");
        var s2Id = course.Sections.Last().Id;
        course.AddCurriculumItem(s2Id, "L2", "Video", ResourceArn.ForVideo(TenantId, Guid.NewGuid()), JsonElement.Parse("{}"));

        // Assert
        l1.BitIndex.Should().Be(0);
        course.Sections.Last().Items.First().BitIndex.Should().Be(1);
    }
}
