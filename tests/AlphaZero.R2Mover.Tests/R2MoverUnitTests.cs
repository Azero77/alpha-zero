using AlphaZero.R2Mover;
using FluentAssertions;
using Xunit;

namespace AlphaZero.R2Mover.Tests;

public class R2MoverUnitTests
{
    [Theory]
    [InlineData("master.m3u8", "application/vnd.apple.mpegurl")]
    [InlineData("video_720p.m3u8", "application/vnd.apple.mpegurl")]
    [InlineData("segment_001.m4s", "video/iso.segment")]
    [InlineData("init.mp4", "video/mp4")]
    [InlineData("source.mp4", "video/mp4")]
    [InlineData("job.json", "application/json")]
    [InlineData("thumbnail.jpg", "image/jpeg")]
    [InlineData("thumbnail.jpeg", "image/jpeg")]
    [InlineData("unknown_file.bin", "application/octet-stream")]
    public void GetContentType_ShouldReturnCorrectMimeType_ForGivenExtension(string filename, string expectedContentType)
    {
        // Act
        var contentType = Program.GetContentType(filename);

        // Assert
        contentType.Should().Be(expectedContentType);
    }
}
