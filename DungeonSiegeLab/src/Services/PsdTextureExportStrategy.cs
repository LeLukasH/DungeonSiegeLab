using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class PsdTextureExportStrategy : ITextureExportStrategy
{
    public static readonly PsdTextureExportStrategy Instance = new();

    private PsdTextureExportStrategy() { }

    public TextureFormat TargetFormat => TextureFormat.Psd;

    public async Task ExportAsync(LoadedTexture texture, string targetPath, Func<LoadedTexture, Task<string>> ensureWorkingPsdAsync)
    {
        var psdPath = await ensureWorkingPsdAsync(texture);
        await Task.Run(() => File.Copy(psdPath, targetPath, overwrite: true));
    }
}
