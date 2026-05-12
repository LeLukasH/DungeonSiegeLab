using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public class BitsLoader : IBitsLoader
{
    public static readonly string UntankPath =
        Path.Combine(AppContext.BaseDirectory, "Untank");

    private readonly TreeCacheService _cache = new() { UntankCacheDirectory = GetUntankAssetsDir() };
    private DiskBitsLoader? _disk;

    public async Task<BitsFolder> LoadAsync(string path, IProgress<(int percent, string folder)>? progress = null)
    {
        if (path.Equals(UntankPath, StringComparison.OrdinalIgnoreCase))
        {
            var cached = await _cache.TryLoadUntankAsync(path);
            if (cached is not null)
                return cached;
        }

        _disk ??= new DiskBitsLoader();
        return await _disk.LoadAsync(path, progress);
    }

    private static string? GetUntankAssetsDir()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets"));
        return Directory.Exists(path) ? path : null;
    }
}
