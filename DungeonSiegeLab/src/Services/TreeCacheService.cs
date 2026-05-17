using System.Text.Json;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Loads the pre-built Untank cache that ships with the app in Assets.
/// No cache is read or written for user-opened Bits folders.
/// </summary>
public class TreeCacheService
{
    /// <summary>
    /// When set, the untank cache is read from this directory instead of
    /// AppContext.BaseDirectory. Set to the source Assets folder in dev so the
    /// cache lives in the repo. Leave null in production — reads from next to the exe.
    /// </summary>
    public string? UntankCacheDirectory { get; set; }

    private string UntankCachePath() =>
        Path.Combine(UntankCacheDirectory ?? AppContext.BaseDirectory, "dslab-cache-untank.json");

    public async Task<BitsFolder?> TryLoadUntankAsync(string untankFolderPath)
    {
        var cachePath = UntankCachePath();
        if (!File.Exists(cachePath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(cachePath);
            var dto  = JsonSerializer.Deserialize<FolderDto>(json);
            return dto?.ToModel(parent: null, basePath: untankFolderPath);
        }
        catch
        {
            return null;
        }
    }

    // ── Relative-path helpers ─────────────────────────────────────────────

    /// <summary>
    /// Returns a slash-separated path relative to basePath.
    /// Root itself → "". Child → "sub/folder/file.gas".
    /// </summary>
    internal static string MakeRelative(string fullPath, string basePath)
    {
        var norm  = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var base_ = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (norm.Equals(base_, StringComparison.OrdinalIgnoreCase))
            return "";
        if (norm.StartsWith(base_, StringComparison.OrdinalIgnoreCase) && norm.Length > base_.Length)
            return norm[(base_.Length + 1)..].Replace('\\', '/');
        return fullPath; // outside base — keep absolute as fallback
    }

    /// <summary>Reconstructs an absolute path from a relative one stored in cache.</summary>
    internal static string MakeAbsolute(string relPath, string basePath)
    {
        var base_ = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relPath == "") return base_;
        // Check if it's already absolute (fallback path stored when outside base)
        if (Path.IsPathRooted(relPath)) return relPath;
        return base_ + Path.DirectorySeparatorChar + relPath.Replace('/', Path.DirectorySeparatorChar);
    }
}
