namespace DungeonSiegeLab.Patterns;

// ============================================================
// STRATEGY PATTERN
// ITextureProcessor definuje algoritmus pre konverziu textúry
// na PNG. Konkrétne stratégie: RawTextureProcessor (cez externý
// RawToPsd nástroj + Magick.NET) a PsdTextureProcessor (priamo
// cez Magick.NET).
// ============================================================

public interface ITextureProcessor
{
    /// <summary>Vráti true ak táto stratégia vie spracovať daný súbor.</summary>
    bool CanProcess(string filePath);

    /// <summary>Konvertuje súbor na PNG a vráti cestu k dočasnému PNG súboru.</summary>
    Task<string> ConvertToPngAsync(string filePath, string rawToPsdToolPath);

    /// <summary>Načíta rozmery textúry.</summary>
    Task<(int Width, int Height)> GetDimensionsAsync(string filePath, string rawToPsdToolPath);
}
