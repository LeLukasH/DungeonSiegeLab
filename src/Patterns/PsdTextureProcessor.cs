using ImageMagick;

namespace DungeonSiegeLab.Patterns;

/// <summary>
/// Stratégia pre .psd textúry – priamo konvertuje cez Magick.NET,
/// bez potreby externého nástroja.
/// </summary>
public class PsdTextureProcessor : ITextureProcessor
{
    public bool CanProcess(string filePath) =>
        Path.GetExtension(filePath).Equals(".psd", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ConvertToPngAsync(string filePath, string rawToPsdToolPath)
    {
        var pngPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");

        await Task.Run(() =>
        {
            using var image = new MagickImage(filePath);
            image.Write(pngPath, MagickFormat.Png);
        });

        return pngPath;
    }

    public async Task<(int Width, int Height)> GetDimensionsAsync(string filePath, string rawToPsdToolPath)
    {
        return await Task.Run(() =>
        {
            using var image = new MagickImage(filePath);
            return ((int)image.Width, (int)image.Height);
        });
    }
}
