using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class RawTextureExportStrategy : ITextureExportStrategy
{
    private readonly Func<string, string, Task> _exportRawAsync;

    public RawTextureExportStrategy(Func<string, string, Task> exportRawAsync)
    {
        _exportRawAsync = exportRawAsync;
    }

    public TextureFormat TargetFormat => TextureFormat.Raw;

    public async Task ExportAsync(LoadedTexture texture, string targetPath, Func<LoadedTexture, Task<string>> ensureWorkingPsdAsync)
    {
        var workingPsdPath = await ensureWorkingPsdAsync(texture);
        await _exportRawAsync(workingPsdPath, targetPath);
    }
}
