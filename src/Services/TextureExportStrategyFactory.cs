using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class TextureExportStrategyFactory
{
    private readonly Dictionary<TextureFormat, ITextureExportStrategy> _strategies;

    public TextureExportStrategyFactory(IEnumerable<ITextureExportStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.TargetFormat);
    }

    public ITextureExportStrategy GetStrategy(TextureFormat targetFormat)
    {
        if (_strategies.TryGetValue(targetFormat, out var strategy))
            return strategy;

        throw new InvalidOperationException($"Unsupported export format: {targetFormat}");
    }
}
