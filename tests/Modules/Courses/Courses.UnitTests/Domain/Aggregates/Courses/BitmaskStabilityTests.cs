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
        course.AssignAssetToCurriculum(v1Id, sectionId, "Lesson 1", JsonDocument.Parse("{}").RootElement);
        course.AssignAssetToCurriculum(a1Id, sectionId, "Assessment 1", JsonDocument.Parse("{}").RootElement);
        course.AssignAssetToCurriculum(v2Id, sectionId, "Lesson 2", JsonDocument.Parse("{}").RootElement);

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

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Video 1");
        course.MarkAssetAvailable(v1Id);

        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Video 2");
        course.MarkAssetAvailable(v2Id);

        course.AssignAssetToCurriculum(v1Id, sectionId, "Lesson 1", JsonDocument.Parse("{}").RootElement);
        course.AssignAssetToCurriculum(v2Id, sectionId, "Lesson 2", JsonDocument.Parse("{}").RootElement);
        
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

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Video 1");
        course.MarkAssetAvailable(v1Id);
        course.AssignAssetToCurriculum(v1Id, s1Id, "L1", JsonDocument.Parse("{}").RootElement);
        
        var l1 = course.Sections.First().Items.First();
        l1.BitIndex.Should().Be(0);

        // Act
        course.AddSection("S2");
        var s2Id = course.Sections.Last().Id;

        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Video 2");
        course.MarkAssetAvailable(v2Id);
        course.AssignAssetToCurriculum(v2Id, s2Id, "L2", JsonDocument.Parse("{}").RootElement);

        // Assert
        l1.BitIndex.Should().Be(0);
        course.Sections.Last().Items.First().BitIndex.Should().Be(1);
    }
}
