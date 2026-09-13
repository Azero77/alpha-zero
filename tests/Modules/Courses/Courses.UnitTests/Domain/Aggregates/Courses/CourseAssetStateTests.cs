using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CourseAssetStateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    private static VideoCourseAsset CreateTestAsset()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        return VideoCourseAsset.Create(id, TenantId, arn, "Test Video", CourseId).Value;
    }

    [Fact]
    public void FullStateCycle_Pending_To_Available_To_InUse_To_Available_To_Archived()
    {
        var asset = CreateTestAsset();
        asset.State.Should().Be(CourseAssetState.Pending);

        // Pending -> Available
        var availResult = asset.MarkAvailable();
        availResult.IsError.Should().BeFalse();
        asset.State.Should().Be(CourseAssetState.Available);

        // Available -> InUse
        var inUseResult = asset.MarkInUse();
        inUseResult.IsError.Should().BeFalse();
        asset.State.Should().Be(CourseAssetState.InUse);

        // InUse -> ReturnToPool (Available)
        var returnResult = asset.ReturnToPool();
        returnResult.IsError.Should().BeFalse();
        asset.State.Should().Be(CourseAssetState.Available);

        // Available -> Archived
        var archiveResult = asset.Archive();
        archiveResult.IsError.Should().BeFalse();
        asset.State.Should().Be(CourseAssetState.Archived);
    }

    [Fact]
    public void Pending_To_Failed_Should_Succeed()
    {
        var asset = CreateTestAsset();

        var result = asset.MarkFailed();

        result.IsError.Should().BeFalse();
        asset.State.Should().Be(CourseAssetState.Failed);
    }

    [Fact]
    public void Archive_Should_Fail_WhenAssetIsInUse()
    {
        var asset = CreateTestAsset();
        asset.MarkAvailable();
        asset.MarkInUse();

        var result = asset.Archive();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
        asset.State.Should().Be(CourseAssetState.InUse);
    }

    [Fact]
    public void MarkInUse_Should_Fail_WhenPending()
    {
        var asset = CreateTestAsset();

        var result = asset.MarkInUse();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public void ReturnToPool_Should_Fail_WhenAvailable()
    {
        var asset = CreateTestAsset();
        asset.MarkAvailable();

        var result = asset.ReturnToPool();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public void MarkAvailable_Should_Fail_WhenAlreadyAvailable()
    {
        var asset = CreateTestAsset();
        asset.MarkAvailable();

        var result = asset.MarkAvailable();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }

    [Fact]
    public void MarkFailed_Should_Fail_WhenAvailable()
    {
        var asset = CreateTestAsset();
        asset.MarkAvailable();

        var result = asset.MarkFailed();

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CourseAsset.State");
    }
}
