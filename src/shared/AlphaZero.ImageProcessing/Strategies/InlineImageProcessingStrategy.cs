using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AlphaZero.ImageProcessing.Strategies;

public class InlineImageProcessingStrategy : BaseImageProcessingStrategy
{
    public override string ProfileName => "inline";

    public override async Task<ImageProcessingResult> ProcessAsync(string inputFilePath, string outputDirectory, CancellationToken ct = default)
    {
        EnsureDirectories(inputFilePath, outputDirectory);

        using var image = await Image.LoadAsync(inputFilePath, ct);

        var result = new ImageProcessingResult
        {
            Width = image.Width,
            Height = image.Height,
            Format = "webp",
            ExifData = ExtractExif(image),
            Variants = new Dictionary<string, string>()
        };

        var variants = new[]
        {
            ("sm", 300),
            ("md", 800),
            ("lg", 1600)
        };

        var encoder = GetWebpEncoder();

        foreach (var (name, maxWidth) in variants)
        {
            string outPath = Path.Combine(outputDirectory, $"{name}.webp");
            
            using var clone = image.Clone(x =>
            {
                if (image.Width > maxWidth)
                {
                    x.Resize(maxWidth, 0); // 0 computes height automatically preserving aspect ratio
                }
            });

            await clone.SaveAsWebpAsync(outPath, encoder, ct);
            result.Variants[name] = outPath;
        }

        return result;
    }
}
