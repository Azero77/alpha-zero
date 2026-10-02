using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace AlphaZero.ImageProcessing;

public class ImageSharpScaler : IImageScaler
{
    public async Task ScaleAsync(string inputPath, string outputPath, ImageScaleOptions options, CancellationToken ct)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found", inputPath);

        using var stream = File.OpenRead(inputPath);
        await ScaleAsync(stream, outputPath, options, ct);
    }

    public async Task ScaleAsync(Stream input, string outputPath, ImageScaleOptions options, CancellationToken ct)
    {
        if (input == null || input.Length == 0)
            throw new ArgumentException("Input stream is empty or null");

        using var image = await Image.LoadAsync(input, ct);

        var resizeOptions = new ResizeOptions
        {
            Size = new Size(options.TargetWidth, options.TargetHeight),
            Mode = options.Mode switch
            {
                ScaleMode.LetterboxPad => ResizeMode.Pad,
                ScaleMode.CenterCrop => ResizeMode.Crop,
                ScaleMode.Stretch => ResizeMode.Stretch,
                _ => ResizeMode.Pad
            }
        };

        if (options.Mode == ScaleMode.LetterboxPad && Color.TryParse(options.PadColor, out var parsedColor))
        {
            resizeOptions.PadColor = parsedColor;
        }

        image.Mutate(x => x.Resize(resizeOptions));

        var encoder = new JpegEncoder
        {
            Quality = options.JpegQuality
        };

        await image.SaveAsJpegAsync(outputPath, encoder, ct);
    }
}
