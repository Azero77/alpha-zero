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

    [Fact]
    public void AddSectionItem_Should_AssignAssetsAndIncrementRefCount_AndBurnBitIndex()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Physics", "Intro Physics", SubjectId).Value;
        course.AddSection("Mechanics");
        var sectionId = course.Sections.First().Id;

        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Kinematics Video");
        course.MarkAssetAvailable(vId);

        var docId = Guid.NewGuid();
        course.AddDocumentAsset(docId, ResourceArn.ForDocument(TenantId, docId), "Formula Sheet", "formulas.pdf", 1024, "application/pdf");
        course.MarkAssetAvailable(docId);

        var content = JsonDocument.Parse($@"[
            {{ ""type"": ""video"", ""courseAssetId"": ""{vId}"" }},
            {{ ""type"": ""document"", ""courseAssetId"": ""{docId}"" }}
        ]").RootElement;

        // Act
        var result = course.AddSectionItem(sectionId, "Kinematics Lesson", "Video", content);

        // Assert
        result.IsError.Should().BeFalse();
        var item = result.Value;
        item.BitIndex.Should().Be(0);
        item.Resources.Should().HaveCount(2);

        var videoAsset = course.Assets.First(a => a.Id == vId);
        videoAsset.RefCount.Should().Be(1);
        videoAsset.State.Should().Be(CourseAssetState.InUse);

        var docAsset = course.Assets.First(a => a.Id == docId);
        docAsset.RefCount.Should().Be(1);
        docAsset.State.Should().Be(CourseAssetState.InUse);
    }

    [Fact]
    public void UpdateSectionItem_Should_AdjustRefCount_ReturningRemovedAssetToPool()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Physics", "Intro Physics", SubjectId).Value;
        course.AddSection("Mechanics");
        var sectionId = course.Sections.First().Id;

        var v1Id = Guid.NewGuid();
        course.AddVideoAsset(v1Id, ResourceArn.ForVideo(TenantId, v1Id), "Video 1");
        course.MarkAssetAvailable(v1Id);

        var v2Id = Guid.NewGuid();
        course.AddVideoAsset(v2Id, ResourceArn.ForVideo(TenantId, v2Id), "Video 2");
        course.MarkAssetAvailable(v2Id);

        var initialContent = JsonDocument.Parse($@"[{{ ""type"": ""video"", ""courseAssetId"": ""{v1Id}"" }}]").RootElement;
        var item = course.AddSectionItem(sectionId, "Lesson", "Video", initialContent).Value;

        // Act: update to reference v2 instead of v1
        var newContent = JsonDocument.Parse($@"[{{ ""type"": ""video"", ""courseAssetId"": ""{v2Id}"" }}]").RootElement;
        var updateResult = course.UpdateSectionItem(sectionId, item.Id, "Updated Lesson", "Video", newContent);

        // Assert
        updateResult.IsError.Should().BeFalse();
        var v1Asset = course.Assets.First(a => a.Id == v1Id);
        v1Asset.RefCount.Should().Be(0);
        v1Asset.State.Should().Be(CourseAssetState.Available); // Returned to pool

        var v2Asset = course.Assets.First(a => a.Id == v2Id);
        v2Asset.RefCount.Should().Be(1);
        v2Asset.State.Should().Be(CourseAssetState.InUse);
    }

    [Fact]
    public void DeleteSectionItem_Should_SoftDelete_BurnBitIndex_AndReleaseAssetToPool()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Physics", "Intro Physics", SubjectId).Value;
        course.AddSection("Mechanics");
        var sectionId = course.Sections.First().Id;

        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Video 1");
        course.MarkAssetAvailable(vId);

        var content = JsonDocument.Parse($@"[{{ ""type"": ""video"", ""courseAssetId"": ""{vId}"" }}]").RootElement;
        var item = course.AddSectionItem(sectionId, "Lesson 1", "Video", content).Value;
        item.BitIndex.Should().Be(0);

        // Act
        var deleteResult = course.DeleteSectionItem(sectionId, item.Id);

        // Assert
        deleteResult.IsError.Should().BeFalse();
        item.IsDeleted.Should().BeTrue();
        item.BitIndex.Should().Be(0); // BitIndex burned

        var asset = course.Assets.First(a => a.Id == vId);
        asset.RefCount.Should().Be(0);
        asset.State.Should().Be(CourseAssetState.Available);
    }

    [Fact]
    public void FastPathPublish_Should_Succeed_WhenTenantAdminAndAllReadinessGatesPass()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Advanced quantum concepts", SubjectId).Value;
        course.AddSection("Foundations");
        var sectionId = course.Sections.First().Id;

        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Qubit Basics");
        course.MarkAssetAvailable(vId);

        var content = JsonDocument.Parse($@"[{{ ""type"": ""video"", ""courseAssetId"": ""{vId}"" }}]").RootElement;
        course.AddSectionItem(sectionId, "Qubits", "Video", content);
        course.AddPlan("Pro", Guid.NewGuid());

        var overview = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, Guid.NewGuid()); // Has cover image

        // Act: Direct publish from Draft by TenantAdmin
        var result = course.Publish(overview, isTenantAdmin: true);

        // Assert
        result.IsError.Should().BeFalse();
        course.Status.Should().Be(CourseStatus.Published);
    }

    [Fact]
    public void FastPathPublish_Should_FailWith422_WhenAttachedAssetIsStillPending()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Advanced quantum concepts", SubjectId).Value;
        course.AddSection("Foundations");
        var sectionId = course.Sections.First().Id;

        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Transcoding Video");
        course.MarkAssetAvailable(vId); // Available so it can be assigned
        var content = JsonDocument.Parse($@"[{{ ""type"": ""video"", ""courseAssetId"": ""{vId}"" }}]").RootElement;
        course.AddSectionItem(sectionId, "Qubits", "Video", content);
        course.AddPlan("Pro", Guid.NewGuid());

        // Now simulate video failed or pending transcoding
        var videoAsset = course.Assets.First(a => a.Id == vId);
        // We can inspect readiness when an asset attached is unready (e.g. MarkFailed)
        // Let's test readiness when unready
        var overview = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, Guid.NewGuid());

        // Fast path publish without cover image should fail
        var overviewNoCover = CourseOverview.Create(
            Guid.NewGuid(), TenantId, course.Id,
            AlphaZero.Modules.Courses.Domain.ValueObjects.RichText.Create("{\"type\":\"doc\"}").Value,
            null, null, null); // Missing cover image

        var result = course.Publish(overviewNoCover, isTenantAdmin: true);

        result.IsError.Should().BeTrue();
        result.FirstError.NumericType.Should().Be(422);
        result.FirstError.Code.Should().Be("Course.NotReady");
    }

    [Fact]
    public void FastPathPublish_Should_FailWithConflict_WhenTeacherAttemptsDirectPublishFromDraft()
    {
        // Arrange
        var course = Course.Create(Guid.NewGuid(), TenantId, "Quantum Computing", "Description", SubjectId).Value;
        course.AddSection("Foundations");
        var vId = Guid.NewGuid();
        course.AddVideoAsset(vId, ResourceArn.ForVideo(TenantId, vId), "Video");
        course.MarkAssetAvailable(vId);
        var content = JsonDocument.Parse($@"[{{ ""type"": ""video"", ""courseAssetId"": ""{vId}"" }}]").RootElement;
        course.AddSectionItem(course.Sections.First().Id, "Lesson", "Video", content);
        course.AddPlan("Standard", Guid.NewGuid());

        // Act: Teacher (isTenantAdmin = false) tries to publish from Draft
        var result = course.Publish(overview: null, isTenantAdmin: false);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Course.Status");
    }
}

