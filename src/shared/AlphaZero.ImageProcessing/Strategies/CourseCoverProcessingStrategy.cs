using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AlphaZero.ImageProcessing.Strategies;

public class CourseCoverProcessingStrategy : BaseImageProcessingStrategy
{
    public override string ProfileName => "course_cover";

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
            ("card", 600, 338, true),
            ("hero", 1200, 675, false)
        };

        foreach (var (name, targetWidth, targetHeight, aggressive) in variants)
        {
            string outPath = Path.Combine(outputDirectory, $"{name}.webp");
            
            using var clone = image.Clone(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new Size(targetWidth, targetHeight),
                    Mode = ResizeMode.Crop
                });
            });

            var encoder = GetWebpEncoder(aggressive);
            await clone.SaveAsWebpAsync(outPath, encoder, ct);
            result.Variants[name] = outPath;
        }

        return result;
    }
}
