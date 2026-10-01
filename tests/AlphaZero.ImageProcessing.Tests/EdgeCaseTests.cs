using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SixLabors.ImageSharp;

namespace AlphaZero.ImageProcessing.Tests;

public class EdgeCaseTests : ImageSharpScalerTests
{
    [Fact]
    public async Task Scale_1x1_EdgeCase()
    {
        var input = CreateDummyImage(1, 1);
        using var ms = new MemoryStream(input);
        var outputPath = Path.GetTempFileName() + ".jpg";

        try
        {
            var options = new ImageScaleOptions { TargetWidth = 10, TargetHeight = 10, Mode = ScaleMode.Stretch };
            await Scaler.ScaleAsync(ms, outputPath, options, CancellationToken.None);

            using var result = await Image.LoadAsync(outputPath);
            Assert.Equal(10, result.Width);
            Assert.Equal(10, result.Height);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Exceptions_On_MissingFile()
    {
        var options = new ImageScaleOptions();
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            Scaler.ScaleAsync("nonexistent.jpg", "out.jpg", options, CancellationToken.None));
    }

    [Fact]
    public async Task Exceptions_On_CorruptStream()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var options = new ImageScaleOptions();
        
        await Assert.ThrowsAnyAsync<Exception>(() => 
            Scaler.ScaleAsync(ms, "out.jpg", options, CancellationToken.None));
    }
}
