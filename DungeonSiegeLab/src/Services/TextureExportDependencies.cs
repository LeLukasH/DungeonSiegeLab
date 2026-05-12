using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public sealed class TextureExportDependencies
{
    public required Func<LoadedTexture, Task<string>> EnsureWorkingPsdAsync { get; init; }

    public required Func<string, string, Task> ExportRawAsync { get; init; }
}
