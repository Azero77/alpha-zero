using System.Collections.Generic;
using AlphaZero.ImageProcessing.Strategies;
using Xunit;

namespace AlphaZero.ImageProcessing.Tests;

public class ImageProcessorFactoryTests
{
    private readonly IImageProcessorFactory _factory;

    public ImageProcessorFactoryTests()
    {
        var strategies = new List<IImageProcessingStrategy>
        {
            new CourseCoverProcessingStrategy(),
            new VideoThumbnailProcessingStrategy(),
            new InlineImageProcessingStrategy(),
            new AvatarProcessingStrategy()
        };

        _factory = new ImageProcessorFactory(strategies);
    }

    [Theory]
    [InlineData("course_cover", typeof(CourseCoverProcessingStrategy))]
    [InlineData("video_thumbnail", typeof(VideoThumbnailProcessingStrategy))]
    [InlineData("inline", typeof(InlineImageProcessingStrategy))]
    [InlineData("avatar", typeof(AvatarProcessingStrategy))]
    public void GetStrategy_ReturnsCorrectStrategy_ForValidProfile(string profile, System.Type expectedType)
    {
        var strategy = _factory.GetStrategy(profile);
        Assert.IsType(expectedType, strategy);
    }

    [Fact]
    public void GetStrategy_ReturnsInlineStrategy_ForUnknownProfile()
    {
        var strategy = _factory.GetStrategy("unknown_profile_123");
        Assert.IsType<InlineImageProcessingStrategy>(strategy);
    }

    [Fact]
    public void GetStrategy_ReturnsInlineStrategy_ForNullOrEmptyProfile()
    {
        Assert.IsType<InlineImageProcessingStrategy>(_factory.GetStrategy(null!));
        Assert.IsType<InlineImageProcessingStrategy>(_factory.GetStrategy(""));
        Assert.IsType<InlineImageProcessingStrategy>(_factory.GetStrategy("   "));
    }
}
