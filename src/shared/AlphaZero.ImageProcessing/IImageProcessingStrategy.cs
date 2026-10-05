using System.Threading;
using System.Threading.Tasks;

namespace AlphaZero.ImageProcessing;

public interface IImageProcessingStrategy
{
    string ProfileName { get; }
    Task<ImageProcessingResult> ProcessAsync(string inputFilePath, string outputDirectory, CancellationToken ct = default);
}
