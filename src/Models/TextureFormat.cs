namespace DungeonSiegeLab.Models;

public enum TextureFormat
{
    Png,
    Psd,
    Raw
}

public static class TextureFormatExtensions
{
    public static TextureFormat FromPath(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => TextureFormat.Png,
            ".psd" => TextureFormat.Psd,
            ".raw" => TextureFormat.Raw,
            _ => throw new InvalidOperationException(
                $"Unsupported texture format: {Path.GetExtension(filePath)}. Supported: .png, .psd, .raw")
        };
    }

    public static string ToExtension(this TextureFormat format) =>
        format switch
        {
            TextureFormat.Png => ".png",
            TextureFormat.Psd => ".psd",
            TextureFormat.Raw => ".raw",
            _ => throw new InvalidOperationException($"Unsupported texture format: {format}")
        };
}
