using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AlphaZero.ImageProcessing.Strategies;

public class AvatarProcessingStrategy : BaseImageProcessingStrategy
{
    public override string ProfileName => "avatar";

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
            ("tiny", 48, 48),
            ("profile", 256, 256)
        };

        var encoder = GetWebpEncoder();

        foreach (var (name, targetWidth, targetHeight) in variants)
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

            await clone.SaveAsWebpAsync(outPath, encoder, ct);
            result.Variants[name] = outPath;
        }

        return result;
    }
}
