using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CourseAssetTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    [Fact]
    public void Create_VideoCourseAsset_Should_Succeed_And_StartInPendingState()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);

        var result = VideoCourseAsset.Create(id, TenantId, arn, "Intro Video", CourseId);

        result.IsError.Should().BeFalse();
        var asset = result.Value;
        asset.Id.Should().Be(id);
        asset.TenantId.Should().Be(TenantId);
        asset.CourseId.Should().Be(CourseId);
        asset.Title.Should().Be("Intro Video");
        asset.State.Should().Be(CourseAssetState.Pending);
        asset.CourseAssetType.Should().Be(CourseAssetType.Video);
    }

    [Fact]
    public void Create_VideoCourseAsset_Should_Fail_WhenTitleIsEmpty()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);

        var result = VideoCourseAsset.Create(id, TenantId, arn, "  ", CourseId);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("VideoCourseAsset.Title");
    }

    [Fact]
    public void Create_DocumentCourseAsset_Should_Succeed_And_StartInPendingState()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForDocument(TenantId, id);

        var result = DocumentCourseAsset.Create(id, TenantId, arn, "Cheatsheet", CourseId, "sheet.pdf", 1024, "application/pdf");

        result.IsError.Should().BeFalse();
        var doc = result.Value;
        doc.FileName.Should().Be("sheet.pdf");
        doc.Size.Should().Be(1024);
        doc.ContentType.Should().Be("application/pdf");
        doc.State.Should().Be(CourseAssetState.Pending);
        doc.CourseAssetType.Should().Be(CourseAssetType.Document);
    }

    [Fact]
    public void Create_DocumentCourseAsset_Should_Fail_WhenFileNameIsEmpty()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForDocument(TenantId, id);

        var result = DocumentCourseAsset.Create(id, TenantId, arn, "Cheatsheet", CourseId, "");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DocumentCourseAsset.FileName");
    }

    [Fact]
    public void Create_AssessmentCourseAsset_Should_Succeed_And_StartInPendingState()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForAssessment(TenantId, id);

        var result = AssessmentCourseAsset.Create(id, TenantId, arn, "Quiz 1", CourseId, 10, AssessmentType.Midterm);

        result.IsError.Should().BeFalse();
        var assessment = result.Value;
        assessment.QuestionsNumber.Should().Be(10);
        assessment.Type.Should().Be(AssessmentType.Midterm);
        assessment.State.Should().Be(CourseAssetState.Pending);
        assessment.CourseAssetType.Should().Be(CourseAssetType.Assessment);
    }

    [Fact]
    public void UpdateStreamingInfo_Should_UpdateProperties()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Intro Video", CourseId).Value;

        asset.UpdateStreamingInfo(TimeSpan.FromMinutes(12), "videos/hls/intro.m3u8", "thumbs/intro.jpg");

        asset.Duration.Should().Be(TimeSpan.FromMinutes(12));
        asset.RelativeStreamingUrl.Should().Be("videos/hls/intro.m3u8");
        asset.ThumbnailUrl.Should().Be("thumbs/intro.jpg");
    }

    [Fact]
    public void UpdateFileInfo_Should_UpdateProperties()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForDocument(TenantId, id);
        var asset = DocumentCourseAsset.Create(id, TenantId, arn, "Sheet", CourseId, "sheet.pdf").Value;

        asset.UpdateFileInfo("updated.pdf", 2048, "application/pdf");

        asset.FileName.Should().Be("updated.pdf");
        asset.Size.Should().Be(2048);
        asset.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public void UpdateAssessmentInfo_Should_UpdateProperties()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForAssessment(TenantId, id);
        var asset = AssessmentCourseAsset.Create(id, TenantId, arn, "Quiz", CourseId).Value;

        asset.UpdateAssessmentInfo(20, AssessmentType.Final);

        asset.QuestionsNumber.Should().Be(20);
        asset.Type.Should().Be(AssessmentType.Final);
    }

    [Fact]
    public void UpdateTitle_Should_UpdateWhenNotEmpty()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Original Title", CourseId).Value;

        asset.UpdateTitle("New Title");
        asset.Title.Should().Be("New Title");

        asset.UpdateTitle("");
        asset.Title.Should().Be("New Title");
    }

    [Fact]
    public void CourseAsset_Should_StartWithZeroRefCount()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;

        asset.RefCount.Should().Be(0);
    }

    [Fact]
    public void AssignToItem_Should_IncrementRefCount_AndTransitionAvailableToInUse()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;
        asset.MarkAvailable();

        var result = asset.AssignToItem();

        result.IsError.Should().BeFalse();
        asset.RefCount.Should().Be(1);
        asset.State.Should().Be(CourseAssetState.InUse);
    }

    [Fact]
    public void AssignToItem_Should_AllowMultiUse_WhenAlreadyInUse()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;
        asset.MarkAvailable();

        asset.AssignToItem();
        var secondAssign = asset.AssignToItem();

        secondAssign.IsError.Should().BeFalse();
        asset.RefCount.Should().Be(2);
        asset.State.Should().Be(CourseAssetState.InUse);
    }

    [Fact]
    public void AssignToItem_Should_Fail_WhenAssetIsPendingOrFailedOrArchived()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var pendingAsset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;

        var result = pendingAsset.AssignToItem();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public void ReleaseFromItem_Should_DecrementRefCount_AndTransitionToAvailable_WhenRefCountReachesZero()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;
        asset.MarkAvailable();

        asset.AssignToItem(); // RefCount = 1
        asset.AssignToItem(); // RefCount = 2

        var firstRelease = asset.ReleaseFromItem();
        firstRelease.IsError.Should().BeFalse();
        asset.RefCount.Should().Be(1);
        asset.State.Should().Be(CourseAssetState.InUse);

        var secondRelease = asset.ReleaseFromItem();
        secondRelease.IsError.Should().BeFalse();
        asset.RefCount.Should().Be(0);
        asset.State.Should().Be(CourseAssetState.Available);
    }

    [Fact]
    public void ReleaseFromItem_Should_Fail_WhenAlreadyReleased_PreventingDoubleRelease()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;
        asset.MarkAvailable();
        asset.AssignToItem();
        asset.ReleaseFromItem(); // Now RefCount == 0, State == Available

        var releaseResult = asset.ReleaseFromItem();

        releaseResult.IsError.Should().BeTrue();
        releaseResult.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public void Archive_Should_Fail_WhenRefCountIsGreaterThanZero()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Video", CourseId).Value;
        asset.MarkAvailable();
        asset.AssignToItem();

        var archiveResult = asset.Archive();

        archiveResult.IsError.Should().BeTrue();
        archiveResult.FirstError.Code.Should().Be("CourseAsset.State");
    }
}

