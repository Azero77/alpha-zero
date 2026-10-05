using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace AlphaZero.ImageProcessing.Strategies;

public abstract class BaseImageProcessingStrategy : IImageProcessingStrategy
{
    public abstract string ProfileName { get; }

    public abstract Task<ImageProcessingResult> ProcessAsync(string inputFilePath, string outputDirectory, CancellationToken ct = default);

    protected void EnsureDirectories(string inputFilePath, string outputDirectory)
    {
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException("Input file not found", inputFilePath);

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
    }

    protected Dictionary<string, string> ExtractExif(Image image)
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

    protected WebpEncoder GetWebpEncoder(bool aggressiveCompression = false)
    {
        return new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = aggressiveCompression ? 60 : 85
        };
    }
}
