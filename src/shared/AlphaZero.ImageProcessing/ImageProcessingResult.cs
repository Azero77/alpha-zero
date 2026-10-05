using System.Collections.Generic;

namespace AlphaZero.ImageProcessing;

public record ImageProcessingResult
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = string.Empty;
    public Dictionary<string, string> ExifData { get; init; } = new();
    
    /// <summary>
    /// Key: variant name (e.g. "thumbnail", "medium", "large")
    /// Value: local file path to the generated WebP file
    /// </summary>
    public Dictionary<string, string> Variants { get; init; } = new();
}
