using System;
using System.Collections.Generic;
using System.Linq;

namespace AlphaZero.ImageProcessing;

public class ImageProcessorFactory : IImageProcessorFactory
{
    private readonly IEnumerable<IImageProcessingStrategy> _strategies;

    public ImageProcessorFactory(IEnumerable<IImageProcessingStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IImageProcessingStrategy GetStrategy(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return GetStrategy("inline");
        }

        var strategy = _strategies.FirstOrDefault(s => s.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        
        return strategy ?? GetStrategy("inline");
    }
}
