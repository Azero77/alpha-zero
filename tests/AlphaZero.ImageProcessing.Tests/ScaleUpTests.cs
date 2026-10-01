using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SixLabors.ImageSharp;

namespace AlphaZero.ImageProcessing.Tests;

public class ScaleUpTests : ImageSharpScalerTests
{
    [Fact]
    public async Task Upscale_360p_To_720p()
    {
        var input = CreateDummyImage(640, 360);
        using var ms = new MemoryStream(input);
        var outputPath = Path.GetTempFileName() + ".jpg";

        try
        {
            var options = new ImageScaleOptions { TargetWidth = 1280, TargetHeight = 720, Mode = ScaleMode.CenterCrop };
            await Scaler.ScaleAsync(ms, outputPath, options, CancellationToken.None);

            using var result = await Image.LoadAsync(outputPath);
            Assert.Equal(1280, result.Width);
            Assert.Equal(720, result.Height);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
