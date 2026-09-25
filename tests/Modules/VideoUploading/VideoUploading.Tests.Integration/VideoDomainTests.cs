using AlphaZero.Modules.VideoUploading.Domain.Events;
using AlphaZero.Modules.VideoUploading.Domain.Models;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace VideoUploading.Tests.Integration;

public class VideoDomainTests
{
    private class FakeClock : IClock
    {
        public DateTime Now => new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => Now;
    }

    [Fact]
    public void UpdateInformation_Should_UpdateTitleAndDescription_AndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        var clock = new FakeClock();
        var videoId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var metadata = new VideoMetadata("lecture.mp4", "video/mp4", 1048576, "HLS");

        var video = Video.Create(
            videoId,
            tenantId,
            "Original Title",
            "Original Description",
            metadata,
            ThumbnailInfo.Empty,
            clock).Value;

        // Act
        var result = video.UpdateInformation("Updated Video Title", "Updated Description");

        // Assert
        result.IsError.Should().BeFalse();
        video.Title.Should().Be("Updated Video Title");
        video.Description.Should().Be("Updated Description");

        var events = video.PopDomainEvents().ToList();
        events.Should().ContainSingle(e => e is VideoMetadataUpdatedDomainEvent);
        var domainEvent = events.OfType<VideoMetadataUpdatedDomainEvent>().Single();
        domainEvent.VideoId.Should().Be(videoId);
        domainEvent.Title.Should().Be("Updated Video Title");
        domainEvent.Description.Should().Be("Updated Description");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateInformation_Should_Fail_WhenTitleIsEmpty(string? title)
    {
        // Arrange
        var clock = new FakeClock();
        var videoId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var metadata = new VideoMetadata("lecture.mp4", "video/mp4", 1048576, "HLS");

        var video = Video.Create(
            videoId,
            tenantId,
            "Original Title",
            "Original Description",
            metadata,
            ThumbnailInfo.Empty,
            clock).Value;

        // Act
        var result = video.UpdateInformation(title!, "New Description");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(VideoErrors.EmptyTitle);
    }
}
