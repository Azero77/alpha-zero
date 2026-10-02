using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AlphaZero.ImageProcessing;

public record ImageScaleOptions
{
    public int TargetWidth { get; init; } = 1280;
    public int TargetHeight { get; init; } = 720;
    public int JpegQuality { get; init; } = 85;
    public ScaleMode Mode { get; init; } = ScaleMode.LetterboxPad;
    public string PadColor { get; init; } = "#000000";
}

public enum ScaleMode { LetterboxPad, CenterCrop, Stretch }

public interface IImageScaler
{
    Task ScaleAsync(string inputPath, string outputPath, ImageScaleOptions options, CancellationToken ct);
    Task ScaleAsync(Stream input, string outputPath, ImageScaleOptions options, CancellationToken ct);
}
