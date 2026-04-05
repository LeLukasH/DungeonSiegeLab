namespace DungeonSiegeLab.Patterns;

// ============================================================
// FACTORY PATTERN
// TextureProcessorFactory vyberie správnu stratégiu (Strategy)
// podľa prípony súboru.
// ============================================================

public class TextureProcessorFactory
{
    private readonly List<ITextureProcessor> _processors;

    public TextureProcessorFactory()
    {
        _processors = new List<ITextureProcessor>
        {
            new RawTextureProcessor(),
            new PsdTextureProcessor()
        };
    }

    /// <summary>
    /// Vráti správny procesor pre daný súbor.
    /// Hodí InvalidOperationException ak formát nie je podporovaný.
    /// </summary>
    public ITextureProcessor GetProcessor(string filePath)
    {
        var processor = _processors.FirstOrDefault(p => p.CanProcess(filePath));

        return processor ?? throw new InvalidOperationException(
            $"Nepodporovaný formát textúry: {Path.GetExtension(filePath)}. " +
            $"Podporované: .raw, .psd");
    }

    public bool IsSupported(string filePath) =>
        _processors.Any(p => p.CanProcess(filePath));
}
