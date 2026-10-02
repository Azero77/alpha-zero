using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using AlphaZero.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AlphaZero.ImageProcessing.Tests;

public class ImageSharpScalerTests
{
    protected IImageScaler Scaler = new ImageSharpScaler();

    protected byte[] CreateDummyImage(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
