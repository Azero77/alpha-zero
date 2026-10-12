using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Domain;
using FluentAssertions;
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
    public void AssignAsset_Should_TransitionAssetToInUse_AndIncrementRefCount()
    {
        var course = CreateTestCourse();
        course.AddSection("Section 1");

        var videoId = Guid.NewGuid();
        var videoArn = ResourceArn.ForVideo(TenantId, videoId);
        course.AddVideoAsset(videoId, videoArn, "Lesson Video");
        course.MarkAssetAvailable(videoId);

        course.PoolAssets.Should().HaveCount(1);

        // Act
        var assignResult = course.AssignAsset(videoId);

        // Assert
        assignResult.IsError.Should().BeFalse();
        var asset = course.Assets.First(a => a.Id == videoId);
        asset.State.Should().Be(CourseAssetState.InUse);
        asset.RefCount.Should().Be(1);
        course.PoolAssets.Should().BeEmpty();
    }

    [Fact]
    public void AssignAsset_Should_Fail_WhenAssetIsNotAvailable()
    {
        var course = CreateTestCourse();
        var videoId = Guid.NewGuid();
        course.AddVideoAsset(videoId, ResourceArn.ForVideo(TenantId, videoId), "Pending Video");

        var result = course.AssignAsset(videoId);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public void ReleaseAsset_Should_DecrementRefCount_AndReturnToPoolWhenZero()
    {
        var course = CreateTestCourse();
        var videoId = Guid.NewGuid();
        course.AddVideoAsset(videoId, ResourceArn.ForVideo(TenantId, videoId), "Lesson Video");
        course.MarkAssetAvailable(videoId);
        course.AssignAsset(videoId);

        course.PoolAssets.Should().BeEmpty();

        // Act
        var releaseResult = course.ReleaseAsset(videoId);

        // Assert
        releaseResult.IsError.Should().BeFalse();
        var asset = course.Assets.First(a => a.Id == videoId);
        asset.State.Should().Be(CourseAssetState.Available);
        asset.RefCount.Should().Be(0);
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
    public void Section_AddItem_Should_AssignOrder_AndStoreInItems()
    {
        var course = CreateTestCourse();
        course.AddSection("Mechanics");
        var section = course.Sections.First();

        var vId = Guid.NewGuid();
        var videoResource = new VideoCurriculumResource(Guid.NewGuid(), 0, vId, vId);

        var bitIndex = course.AllocateNextBitIndex();
        var result = section.AddItem(Guid.NewGuid(), TenantId, "Kinematics Lesson", "Video", bitIndex, new[] { videoResource });

        result.IsError.Should().BeFalse();
        var item = result.Value;
        item.BitIndex.Should().Be(0);
        item.Order.Should().Be(0);
        section.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Section_UpdateItem_Should_UpdateItemFields()
    {
        var course = CreateTestCourse();
        course.AddSection("Mechanics");
        var section = course.Sections.First();

        var bitIndex = course.AllocateNextBitIndex();
        var item = section.AddItem(Guid.NewGuid(), TenantId, "Lesson 1", "Video", bitIndex).Value;

        var newResource = new ArticleCurriculumResource(Guid.NewGuid(), 0, "Updated Content");
        var updateResult = section.UpdateItem(item.Id, "Updated Lesson 1", "Article", new[] { newResource });

        updateResult.IsError.Should().BeFalse();
        item.Title.Should().Be("Updated Lesson 1");
        item.MainType.Should().Be("Article");
        item.Resources.Should().HaveCount(1);
    }

    [Fact]
    public void Section_RemoveItem_Should_SoftDeleteItem()
    {
        var course = CreateTestCourse();
        course.AddSection("Mechanics");
        var section = course.Sections.First();

        var bitIndex = course.AllocateNextBitIndex();
        var item = section.AddItem(Guid.NewGuid(), TenantId, "Lesson 1", "Video", bitIndex).Value;

        var removeResult = section.RemoveItem(item.Id);

        removeResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void CoursePublishingService_DirectPublish_Should_Succeed_WhenCanDirectPublishAndReady()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Advanced quantum concepts", SubjectId).Value;
        course.AddSection("Foundations");
        var section = course.Sections.First();

        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Qubit Basics");
        course.MarkAssetAvailable(vId);
        course.AssignAsset(vId);

        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, vId, vId);
        section.AddItem(Guid.NewGuid(), TenantId, "Qubits", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.AddPlan("Pro", Guid.NewGuid());

        var overview = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, Guid.NewGuid()); // Has cover image

        var service = new CoursePublishingService(new CourseReadinessService());

        // Act
        var result = service.Publish(course, overview, canDirectPublish: true);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
    }

    [Fact]
    public void CoursePublishingService_DirectPublish_Should_Fail_WhenOverviewMissingCover()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Advanced quantum concepts", SubjectId).Value;
        course.AddSection("Foundations");
        var section = course.Sections.First();

        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Qubit Basics");
        course.MarkAssetAvailable(vId);
        course.AssignAsset(vId);

        var resource = new VideoCurriculumResource(Guid.NewGuid(), 0, vId, vId);
        section.AddItem(Guid.NewGuid(), TenantId, "Qubits", "Video", course.AllocateNextBitIndex(), new[] { resource });
        course.AddPlan("Pro", Guid.NewGuid());

        var overviewNoCover = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, null); // Missing cover image

        var service = new CoursePublishingService(new CourseReadinessService());

        var result = service.Publish(course, overviewNoCover, canDirectPublish: true);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.NotReady");
    }

    [Fact]
    public void CoursePublishingService_DirectPublish_Should_FailWithConflict_WhenNotAuthorized()
    {
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Description", SubjectId).Value;
        course.AddSection("Foundations");
        course.AddPlan("Standard", Guid.NewGuid());

        var service = new CoursePublishingService(new CourseReadinessService());

        var result = service.Publish(course, overview: null, canDirectPublish: false);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
    }
}
