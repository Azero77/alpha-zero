using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CourseAggregateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static Course CreateTestCourse()
    {
        return Course.Create(Guid.NewGuid(), TenantId, "Math 101", "Basics", SubjectId).Value;
    }

    [Fact]
    public void AddAssetMethods_Should_AddAssets_Idempotently()
    {
        var course = CreateTestCourse();
        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(TenantId, videoId);

        var first = course.AddVideoAsset(videoId, videoArn, "Intro");
        first.IsError.Should().BeFalse();
        course.Assets.Should().HaveCount(1);

        // Second call with same ID is idempotent
        var second = course.AddVideoAsset(videoId, videoArn, "Intro Duplicate");
        second.IsError.Should().BeFalse();
        second.Value.Id.Should().Be(videoId);
        course.Assets.Should().HaveCount(1);
    }

    [Fact]
    public void AssignAssetToCurriculum_Should_CreateItem_And_TransitionAssetToInUse()
    {
        var course = CreateTestCourse();
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(TenantId, videoId);
        course.AddVideoAsset(videoId, videoArn, "Lesson Video");
        course.MarkAssetAvailable(videoId);

        course.PoolAssets.Should().HaveCount(1);

        // Act
        var result = course.AssignAssetToCurriculum(videoId, sectionId, "Lesson 1", JsonDocument.Parse("{}").RootElement);

        // Assert
        result.IsError.Should().BeFalse();
        var item = result.Value;
        item.Title.Should().Be("Lesson 1");
        item.BitIndex.Should().Be(0);
        course.TotalTrackedItems.Should().Be(1);

        var asset = course.Assets.First(a => a.Id == videoId);
        asset.State.Should().Be(CourseAssetState.InUse);
        course.PoolAssets.Should().BeEmpty();
    }

    [Fact]
    public void AssignAssetToCurriculum_Should_Fail_WhenAssetIsNotAvailable()
    {
        var course = CreateTestCourse();
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var videoId = Guid.NewGuid();
        course.AddVideoAsset(videoId, ResourceArn.ForVideo(TenantId, videoId), "Pending Video");
        // Still in Pending state

        var result = course.AssignAssetToCurriculum(videoId, sectionId, "Lesson 1", JsonDocument.Parse("{}").RootElement);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.NotAvailable");
    }

    [Fact]
    public void UnassignFromCurriculum_Should_ReturnAssetsToPool_And_SoftDeleteItem()
    {
        var course = CreateTestCourse();
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var videoId = Guid.NewGuid();
        course.AddVideoAsset(videoId, ResourceArn.ForVideo(TenantId, videoId), "Lesson Video");
        course.MarkAssetAvailable(videoId);
        var item = course.AssignAssetToCurriculum(videoId, sectionId, "Lesson 1", JsonDocument.Parse("{}").RootElement).Value;

        course.PoolAssets.Should().BeEmpty();

        // Act
        var unassignResult = course.UnassignFromCurriculum(item.Id);

        // Assert
        unassignResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();

        var asset = course.Assets.First(a => a.Id == videoId);
        asset.State.Should().Be(CourseAssetState.Available);
        course.PoolAssets.Should().HaveCount(1);
    }

    [Fact]
    public void DismissAsset_Should_ArchiveAvailableAsset()
    {
        var course = CreateTestCourse();
        var videoId = Guid.NewGuid();
        course.AddVideoAsset(videoId, ResourceArn.ForVideo(TenantId, videoId), "Unused Video");
        course.MarkAssetAvailable(videoId);

        var result = course.DismissAsset(videoId);

        result.IsError.Should().BeFalse();
        course.Assets.First(a => a.Id == videoId).State.Should().Be(CourseAssetState.Archived);
        course.PoolAssets.Should().BeEmpty();
    }

    [Fact]
    public void Publish_Should_Succeed_EvenWhenAssetsRemainInPool()
    {
        var course = CreateTestCourse();
        course.AddSection("Section 1");
        var sectionId = course.Sections.First().Id;

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Assigned Video");
        course.MarkAssetAvailable(v1Id);
        course.AssignAssetToCurriculum(v1Id, sectionId, "Lesson 1", JsonDocument.Parse("{}").RootElement);

        // Staged/upcoming asset left in pool
        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Pool Video for Future");
        course.MarkAssetAvailable(v2Id);

        course.AddPlan("Standard", Guid.NewGuid());
        course.SubmitForReview();
        course.Approve();

        // Act
        var result = course.Publish();

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
        course.PoolAssets.Should().HaveCount(1);
    }
}
