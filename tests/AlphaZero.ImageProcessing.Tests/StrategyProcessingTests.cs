using System;
using System.IO;
using System.Threading.Tasks;
using AlphaZero.ImageProcessing.Strategies;
using SixLabors.ImageSharp;
using Xunit;

namespace AlphaZero.ImageProcessing.Tests;

public class StrategyProcessingTests : IDisposable
{
    private readonly string _inputFilePath;
    private readonly string _outputDir;

    public StrategyProcessingTests()
    {
        _inputFilePath = TestHelper.CreateDummyImageFile(1920, 1080);
        _outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public async Task CourseCoverStrategy_ProducesCorrectVariants()
    {
        var strategy = new CourseCoverProcessingStrategy();
        var result = await strategy.ProcessAsync(_inputFilePath, _outputDir);

        Assert.Equal(2, result.Variants.Count);
        Assert.Contains("card", result.Variants.Keys);
        Assert.Contains("hero", result.Variants.Keys);

        using var card = await Image.LoadAsync(result.Variants["card"]);
        Assert.Equal(600, card.Width);
        Assert.Equal(338, card.Height);

        using var hero = await Image.LoadAsync(result.Variants["hero"]);
        Assert.Equal(1200, hero.Width);
        Assert.Equal(675, hero.Height);
    }

    [Fact]
    public async Task VideoThumbnailStrategy_ProducesCorrectVariants()
    {
        var strategy = new VideoThumbnailProcessingStrategy();
        var result = await strategy.ProcessAsync(_inputFilePath, _outputDir);

        Assert.Equal(2, result.Variants.Count);
        Assert.Contains("sidebar", result.Variants.Keys);
        Assert.Contains("player", result.Variants.Keys);

        using var sidebar = await Image.LoadAsync(result.Variants["sidebar"]);
        Assert.Equal(160, sidebar.Width);
        Assert.Equal(90, sidebar.Height);

        using var player = await Image.LoadAsync(result.Variants["player"]);
        Assert.Equal(1280, player.Width);
        Assert.Equal(720, player.Height);
    }

    [Fact]
    public async Task InlineImageStrategy_ProducesCorrectVariants_MaintainsAspectRatio()
    {
        var strategy = new InlineImageProcessingStrategy();
        var result = await strategy.ProcessAsync(_inputFilePath, _outputDir);

        Assert.Equal(3, result.Variants.Count);
        Assert.Contains("sm", result.Variants.Keys);
        Assert.Contains("md", result.Variants.Keys);
        Assert.Contains("lg", result.Variants.Keys);

        // 1920x1080 input (16:9) -> height should scale down proportionally
        using var sm = await Image.LoadAsync(result.Variants["sm"]);
        Assert.Equal(300, sm.Width);
        Assert.Equal((int)Math.Round(300 * 1080.0 / 1920.0), sm.Height); // 169

        using var md = await Image.LoadAsync(result.Variants["md"]);
        Assert.Equal(800, md.Width);
        Assert.Equal(450, md.Height);

        using var lg = await Image.LoadAsync(result.Variants["lg"]);
        Assert.Equal(1600, lg.Width);
        Assert.Equal(900, lg.Height);
    }

    [Fact]
    public async Task AvatarStrategy_ProducesCorrectVariants()
    {
        var strategy = new AvatarProcessingStrategy();
        var result = await strategy.ProcessAsync(_inputFilePath, _outputDir);

        Assert.Equal(2, result.Variants.Count);
        Assert.Contains("tiny", result.Variants.Keys);
        Assert.Contains("profile", result.Variants.Keys);

        using var tiny = await Image.LoadAsync(result.Variants["tiny"]);
        Assert.Equal(48, tiny.Width);
        Assert.Equal(48, tiny.Height);

        using var profile = await Image.LoadAsync(result.Variants["profile"]);
        Assert.Equal(256, profile.Width);
        Assert.Equal(256, profile.Height);
    }

    public void Dispose()
    {
        if (File.Exists(_inputFilePath))
            File.Delete(_inputFilePath);

        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
    }
}
