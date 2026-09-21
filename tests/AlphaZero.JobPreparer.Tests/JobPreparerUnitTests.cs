using System.Security.Cryptography;
using System.Text;
using AlphaZero.JobPreparer;
using FluentAssertions;
using Xunit;

namespace AlphaZero.JobPreparer.Tests;

public class JobPreparerUnitTests
{
    [Theory]
    [InlineData(640, 360, 1)]      // 360p only
    [InlineData(854, 480, 2)]      // 360p, 480p
    [InlineData(1280, 720, 3)]     // 360p, 480p, 720p
    [InlineData(1920, 1080, 4)]    // 360p, 480p, 720p, 1080p
    [InlineData(3840, 2160, 4)]    // Clamped up to 1080p
    public void BuildAdaptiveLadder_ShouldIncludeExpectedRenditions_BasedOnHeight(int width, int height, int expectedRenditionCount)
    {
        // Act
        var ladder = Function.BuildAdaptiveLadder(width, height);

        // Assert
        ladder.Should().NotBeNull();
        ladder.Length.Should().Be(expectedRenditionCount);
        ladder[0].name.Should().Be("360p");

        if (expectedRenditionCount >= 2)
            ladder[1].name.Should().Be("480p");
        if (expectedRenditionCount >= 3)
            ladder[2].name.Should().Be("720p");
        if (expectedRenditionCount >= 4)
            ladder[3].name.Should().Be("1080p");
    }

    [Fact]
    public void GenerateClearKeySecret_ShouldBeDeterministic_AndCorrectLength()
    {
        // Arrange
        var masterSecret = "super-secure-master-secret-key-12345";
        var videoId = "8a2f4c9b-1122-3344-5566-778899aabbcc";

        // Act
        var key1 = Function.GenerateClearKeySecret(masterSecret, videoId);
        var key2 = Function.GenerateClearKeySecret(masterSecret, videoId);

        // Assert
        key1.Should().NotBeNullOrWhiteSpace();
        key1.Length.Should().Be(32); // 16 bytes in hex = 32 chars
        key1.Should().Be(key2);

        // Verify HMAC calculation matches standard HMACSHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(masterSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(videoId));
        var expectedHex = Convert.ToHexString(hash[..16]).ToLowerInvariant();
        key1.Should().Be(expectedHex);
    }
}
