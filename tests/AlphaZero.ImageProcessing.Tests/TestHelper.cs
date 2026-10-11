using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AlphaZero.ImageProcessing.Tests;

public static class TestHelper
{
    public static string CreateDummyImageFile(int width, int height)
    {
        string path = Path.GetTempFileName() + ".png";
        using var image = new Image<Rgba32>(width, height);
        image.SaveAsPng(path);
        return path;
    }
}
