namespace AlphaZero.ImageProcessing;

public interface IImageProcessorFactory
{
    IImageProcessingStrategy GetStrategy(string profileName);
}
