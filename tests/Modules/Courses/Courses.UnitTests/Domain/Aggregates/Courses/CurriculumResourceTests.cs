using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Aggregates.Courses;

public class CurriculumResourceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    [Fact]
    public void Constructor_Should_SetPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Test Video", CourseId).Value;
        var metadata = JsonDocument.Parse("{\"key\":\"value\"}").RootElement;

        var resource = new CurriculumResource(asset, 0, metadata);

        resource.CourseAssetId.Should().Be(id);
        resource.Asset.Should().Be(asset);
        resource.Order.Should().Be(0);
        resource.Metadata.GetProperty("key").GetString().Should().Be("value");
    }

    [Fact]
    public void UpdateOrder_Should_Succeed_WhenOrderIsZeroOrPositive()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Test Video", CourseId).Value;
        var resource = new CurriculumResource(asset, 0, JsonDocument.Parse("{}").RootElement);

        var result = resource.UpdateOrder(2);

        result.IsError.Should().BeFalse();
        resource.Order.Should().Be(2);
    }

    [Fact]
    public void UpdateOrder_Should_Fail_WhenOrderIsNegative()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Test Video", CourseId).Value;
        var resource = new CurriculumResource(asset, 0, JsonDocument.Parse("{}").RootElement);

        var result = resource.UpdateOrder(-1);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Courses.Resources.Order.Validation");
    }

    [Fact]
    public void UpdateMetadata_Should_UpdateMetadataProperty()
    {
        var id = Guid.NewGuid();
        var arn = ResourceArn.ForVideo(TenantId, id);
        var asset = VideoCourseAsset.Create(id, TenantId, arn, "Test Video", CourseId).Value;
        var resource = new CurriculumResource(asset, 0, JsonDocument.Parse("{}").RootElement);

        var newMetadata = JsonDocument.Parse("{\"updated\":true}").RootElement;
        resource.UpdateMetadata(newMetadata);

        resource.Metadata.GetProperty("updated").GetBoolean().Should().BeTrue();
    }
}
