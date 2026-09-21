using AlphaZero.VideoAnalyzer;
using FluentAssertions;
using Xunit;

namespace AlphaZero.VideoAnalyzer.Tests;

public class VideoAnalyzerUnitTests
{
    [Theory]
    [InlineData("30000/1001", 29.97)]
    [InlineData("24000/1001", 23.98)]
    [InlineData("60000/1001", 59.94)]
    [InlineData("24/1", 24.0)]
    [InlineData("25/1", 25.0)]
    [InlineData("30/1", 30.0)]
    [InlineData("60/1", 60.0)]
    [InlineData("24", 24.0)]
    [InlineData("29.97", 29.97)]
    [InlineData(null, 30.0)]
    [InlineData("", 30.0)]
    [InlineData("invalid_fps", 30.0)]
    [InlineData("100/0", 30.0)]
    public void ParseFrameRate_ShouldParseVariousFormatsCorrectly(string? input, double expectedFps)
    {
        var result = Function.ParseFrameRate(input);
        result.Should().Be(expectedFps);
    }

    [Fact]
    public void ParseFfprobeOutput_ShouldExtractVideoAndAudioMetadataProperly()
    {
        // Arrange
        var mockFfprobeJson = """
        {
          "streams": [
            {
              "codec_type": "video",
              "codec_name": "h264",
              "width": 1920,
              "height": 1080,
              "r_frame_rate": "30000/1001"
            },
            {
              "codec_type": "audio",
              "codec_name": "aac",
              "sample_rate": "48000",
              "bit_rate": "192000"
            }
          ],
          "format": {
            "duration": "3665.500000"
          }
        }
        """;

        // Act
        var output = Function.ParseFfprobeOutput(mockFfprobeJson);

        // Assert
        output.Should().NotBeNull();
        output.SourceWidth.Should().Be(1920);
        output.SourceHeight.Should().Be(1080);
        output.VideoCodec.Should().Be("h264");
        output.AudioCodec.Should().Be("aac");
        output.FrameRate.Should().Be(29.97);
        output.AudioSampleRate.Should().Be(48000);
        output.AudioBitrateKbps.Should().Be(192);
        output.DurationSeconds.Should().BeApproximately(3665.5, 0.01);
        output.DurationFormatted.Should().Be("01:01:05");
        output.AspectRatio.Should().Be("16:9");
    }

    [Fact]
    public void ParseFfprobeOutput_ShouldHandleNonStandardAspectRatio()
    {
        // Arrange
        var mockFfprobeJson = """
        {
          "streams": [
            {
              "codec_type": "video",
              "codec_name": "vp9",
              "width": 800,
              "height": 600,
              "r_frame_rate": "25/1"
            }
          ],
          "format": {
            "duration": "10.0"
          }
        }
        """;

        // Act
        var output = Function.ParseFfprobeOutput(mockFfprobeJson);

        // Assert
        output.Should().NotBeNull();
        output.SourceWidth.Should().Be(800);
        output.SourceHeight.Should().Be(600);
        output.AspectRatio.Should().Be("800:600");
        output.DurationFormatted.Should().Be("00:00:10");
    }
}
