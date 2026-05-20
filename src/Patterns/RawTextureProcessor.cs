using ImageMagick;

namespace DungeonSiegeLab.Patterns;

/// <summary>
/// Stratégia pre .raw textúry z Dungeon Siege.
/// Kroky: .raw → .psd (cez externý RawToPsd.exe) → .png (cez Magick.NET).
/// </summary>
public class RawTextureProcessor : ITextureProcessor
{
    public bool CanProcess(string filePath) =>
        Path.GetExtension(filePath).Equals(".raw", StringComparison.OrdinalIgnoreCase);

    public async Task<string> ConvertToPngAsync(string filePath, string rawToPsdToolPath)
    {
        var psdPath = await ConvertRawToPsdAsync(filePath, rawToPsdToolPath);
        return await ConvertPsdToPngAsync(psdPath);
    }

    public async Task<(int Width, int Height)> GetDimensionsAsync(string filePath, string rawToPsdToolPath)
    {
        var pngPath = await ConvertToPngAsync(filePath, rawToPsdToolPath);
        using var image = new MagickImage(pngPath);
        return ((int)image.Width, (int)image.Height);
    }

    // --- Privátne metódy ---

    private static async Task<string> ConvertRawToPsdAsync(string rawPath, string toolPath)
    {
        if (!File.Exists(toolPath))
            throw new FileNotFoundException($"RawToPsd nástroj nebol nájdený: {toolPath}");

        var psdPath = Path.ChangeExtension(Path.GetTempFileName(), ".psd");

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = $"\"{rawPath}\" \"{psdPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"RawToPsd zlyhalo (exit {process.ExitCode}): {error}");
        }

        return psdPath;
    }

    private static async Task<string> ConvertPsdToPngAsync(string psdPath)
    {
        var pngPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");

        await Task.Run(() =>
        {
            using var image = new MagickImage(psdPath);
            image.Write(pngPath, MagickFormat.Png);
        });

        // Uprac dočasný PSD súbor
        if (File.Exists(psdPath))
            File.Delete(psdPath);

        return pngPath;
    }
}
