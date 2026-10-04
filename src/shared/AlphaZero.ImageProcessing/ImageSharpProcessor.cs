using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace AlphaZero.ImageProcessing;

public class ImageSharpProcessor : IImageProcessor
{
    public async Task<ImageProcessingResult> ProcessAsync(string inputFilePath, string outputDirectory, CancellationToken ct = default)
    {
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException("Input file not found", inputFilePath);

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        // Required: Single-pass loading
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
            ("thumbnail", 300),
            ("medium", 800),
            ("large", 1600)
        };

        var encoder = new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = 85
        };

        foreach (var (name, targetWidth) in variants)
        {
            string outPath = Path.Combine(outputDirectory, $"{name}.webp");
            
            using var clone = image.Clone(x =>
            {
                if (image.Width > targetWidth)
                {
                    x.Resize(targetWidth, 0); // 0 computes height automatically preserving aspect ratio
                }
            });

            await clone.SaveAsWebpAsync(outPath, encoder, ct);
            result.Variants[name] = outPath;
        }

        return result;
    }

    private Dictionary<string, string> ExtractExif(Image image)
    {
        var dict = new Dictionary<string, string>();
        var profile = image.Metadata.ExifProfile;
        
        if (profile != null)
        {
            foreach (var value in profile.Values)
            {
                var valStr = value.GetValue()?.ToString();
                if (!string.IsNullOrEmpty(valStr))
                {
                    dict[value.Tag.ToString()] = valStr;
                }
            }
        }

        return dict;
    }
}
