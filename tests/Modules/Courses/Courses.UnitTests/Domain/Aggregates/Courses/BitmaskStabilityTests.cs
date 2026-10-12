using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class BitmaskStabilityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static CurriculumItem AddItem(Course course, CourseSection section, string title, string type)
    {
        var bitIndex = course.AllocateNextBitIndex();
        return section.AddItem(Guid.NewGuid(), TenantId, title, type, bitIndex).Value;
    }

    [Fact]
    public void BitIndex_Should_BeAssignedSequentially_WhenItemsAreAdded()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Architecture 101", null, SubjectId).Value;
        course.AddSection("Section 1");
        var section = course.Sections.First();

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Video 1");
        course.MarkAssetAvailable(v1Id);

        var a1Id = Guid.NewGuid();
        course.AddAssessmentAsset(a1Id, ResourceArn.ForAssessment(TenantId, a1Id), "Quiz 1", 5, AssessmentType.Practice);
        course.MarkAssetAvailable(a1Id);

        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Video 2");
        course.MarkAssetAvailable(v2Id);

        // Act
        AddItem(course, section, "Lesson 1", "Video");
        AddItem(course, section, "Assessment 1", "Quiz");
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

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Video 1");
        course.MarkAssetAvailable(v1Id);

        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Video 2");
        course.MarkAssetAvailable(v2Id);

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

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Video 1");
        course.MarkAssetAvailable(v1Id);
        var l1 = AddItem(course, s1, "L1", "Video");
        
        l1.BitIndex.Should().Be(0);

        // Act
        course.AddSection("S2");
        var s2 = course.Sections.Last();

        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Video 2");
        course.MarkAssetAvailable(v2Id);
        var l2 = AddItem(course, s2, "L2", "Video");

        // Assert
        l1.BitIndex.Should().Be(0);
        l2.BitIndex.Should().Be(1);
    }
}
