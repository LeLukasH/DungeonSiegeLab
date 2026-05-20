namespace DungeonSiegeLab.Models;

public class LoadedTexture
{
    public string Name { get; init; } = "";
    public string OriginalPath { get; init; } = "";
    public TextureFormat OriginalFormat { get; init; }
    public string? PngCachePath { get; set; }
    public string? WorkingPsdPath { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    public bool IsOk => Width > 0 && Height > 0 && Width % 16 == 0 && Height % 16 == 0;

    public string StatusText => IsOk ? "OK" : "NOT OK - dimensions are not divisible by 16";

    public List<string> ReferencedByTemplates { get; set; } = new();

    public string StandardUsage =>
        ReferencedByTemplates.Count > 0
            ? string.Join(", ", ReferencedByTemplates)
            : "-";
}
