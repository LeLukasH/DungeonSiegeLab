using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public interface ITextureExportStrategy
{
    TextureFormat TargetFormat { get; }

    Task ExportAsync(LoadedTexture texture, string targetPath, TextureExportDependencies dependencies);
}
