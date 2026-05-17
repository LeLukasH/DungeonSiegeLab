using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class PsdTextureExportStrategy : ITextureExportStrategy
{
    public static readonly PsdTextureExportStrategy Instance = new();

    private PsdTextureExportStrategy() { }

    public TextureFormat TargetFormat => TextureFormat.Psd;

    public async Task ExportAsync(LoadedTexture texture, string targetPath, TextureExportDependencies dependencies)
    {
        var psdPath = await dependencies.EnsureWorkingPsdAsync(texture);
        await Task.Run(() => File.Copy(psdPath, targetPath, overwrite: true));
    }
}
