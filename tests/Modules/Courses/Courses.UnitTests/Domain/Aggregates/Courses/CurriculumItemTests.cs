using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CurriculumItemTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    [Fact]
    public void AddResource_Should_Succeed_WhenAssetTypeMatchesMainType()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(TenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, TenantId, videoArn, "Intro", CourseId).Value;

        var result = item.AddResource(asset, JsonDocument.Parse("{}").RootElement);

        result.IsError.Should().BeFalse();
        item.Resources.Should().HaveCount(1);
        item.Resources.First().CourseAssetId.Should().Be(videoId);
    }

    [Fact]
    public void AddResource_Should_Fail_WhenAssetTypeDoesNotMatchMainType()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        var docId = Guid.NewGuid();
        var docArn = ResourceArn.ForDocument(TenantId, docId);
        var asset = DocumentCourseAsset.Create(docId, TenantId, docArn, "Notes", CourseId, "notes.pdf").Value;

        var result = item.AddResource(asset, JsonDocument.Parse("{}").RootElement);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumItem.TypeMismatch");
        item.Resources.Should().BeEmpty();
    }

    [Fact]
    public void AddResource_Should_Fail_WhenTenantMismatches()
    {
        var otherTenantId = Guid.NewGuid();
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(otherTenantId, videoId);
        var asset = VideoCourseAsset.Create(videoId, otherTenantId, videoArn, "Intro", CourseId).Value;

        var result = item.AddResource(asset, JsonDocument.Parse("{}").RootElement);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CurriculumItem.TenantMismatch");
        item.Resources.Should().BeEmpty();
    }

    [Fact]
    public void ReorderResources_Should_ReorderResourcesByAssetId()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");

        var v1Id = Guid.NewGuid();
        var v1 = VideoCourseAsset.Create(v1Id, TenantId, ResourceArn.ForVideo(TenantId, v1Id), "V1", CourseId).Value;
        var v2Id = Guid.NewGuid();
        var v2 = VideoCourseAsset.Create(v2Id, TenantId, ResourceArn.ForVideo(TenantId, v2Id), "V2", CourseId).Value;

        item.AddResource(v1, JsonDocument.Parse("{}").RootElement);
        item.AddResource(v2, JsonDocument.Parse("{}").RootElement);

        item.Resources.First().CourseAssetId.Should().Be(v1Id);

        // Reorder v2 first
        item.ReorderResources(new List<Guid> { v2Id, v1Id });

        var resources = item.Resources.ToList();
        resources[0].CourseAssetId.Should().Be(v2Id);
        resources[0].Order.Should().Be(0);
        resources[1].CourseAssetId.Should().Be(v1Id);
        resources[1].Order.Should().Be(1);
    }

    [Fact]
    public void Delete_And_Restore_Should_ManageLifecycle()
    {
        var item = new CurriculumItem(Guid.NewGuid(), TenantId, SectionId, "Lesson 1", 0, 0, "Video");
        item.IsDeleted.Should().BeFalse();

        var delResult = item.Delete();
        delResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();
        item.OnDeleted.Should().NotBeNull();

        // Double delete fails
        item.Delete().IsError.Should().BeTrue();

        var restResult = item.Restore();
        restResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeFalse();
        item.OnDeleted.Should().BeNull();
    }
}
