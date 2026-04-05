namespace DungeonSiegeLab.Models;

public class LoadedTexture
{
    public string Name { get; init; } = "";

    /// <summary>Pôvodná cesta k .raw alebo .psd súboru.</summary>
    public string OriginalPath { get; init; } = "";

    /// <summary>Dočasná cesta k PNG verzi pre zobrazenie.</summary>
    public string? PngCachePath { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Status: OK len ak Width a Height sú deliteľné 16.</summary>
    public bool IsOk => Width > 0 && Height > 0 && Width % 16 == 0 && Height % 16 == 0;

    public string StatusText => IsOk ? "OK" : "NOT OK – rozmery nie sú deliteľné 16";

    /// <summary>Kde sa táto textúra typicky používa (zistí TextureFinder).</summary>
    public List<string> ReferencedByTemplates { get; set; } = new();

    public string StandardUsage =>
        ReferencedByTemplates.Count > 0
            ? string.Join(", ", ReferencedByTemplates)
            : "–";
}
