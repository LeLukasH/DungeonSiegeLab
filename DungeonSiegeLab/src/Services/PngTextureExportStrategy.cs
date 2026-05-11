using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class PngTextureExportStrategy : ITextureExportStrategy
{
    public static readonly PngTextureExportStrategy Instance = new();

    private PngTextureExportStrategy() { }

    public TextureFormat TargetFormat => TextureFormat.Png;

    public async Task ExportAsync(LoadedTexture texture, string targetPath, Func<LoadedTexture, Task<string>> ensureWorkingPsdAsync)
    {
        if (texture.PngCachePath is null)
            throw new InvalidOperationException("Texture preview is not available.");

        await Task.Run(() => File.Copy(texture.PngCachePath, targetPath, overwrite: true));
    }
}
