using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class RawTextureExportStrategy : ITextureExportStrategy
{
    public static readonly RawTextureExportStrategy Instance = new();

    private RawTextureExportStrategy() { }

    public TextureFormat TargetFormat => TextureFormat.Raw;

    public async Task ExportAsync(LoadedTexture texture, string targetPath, TextureExportDependencies dependencies)
    {
        var workingPsdPath = await dependencies.EnsureWorkingPsdAsync(texture);
        await dependencies.ExportRawAsync(workingPsdPath, targetPath);
    }
}
