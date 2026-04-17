using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Caches a fully-parsed BitsFolder tree as JSON next to the executable.
/// Paths are stored relative to the root folder, so cache files are portable
/// across machines (e.g. a pre-built Untank cache can ship with the app).
///
/// Cache filename:
///   - If cacheKey is provided: dslab-cache-{cacheKey}.json  (fixed, predictable)
///   - Otherwise:               dslab-cache-{folderName}-{MD5[..12]}.json
/// </summary>
public class TreeCacheService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    /// <summary>
    /// When set, the "untank" cache is read from and written to this directory instead of
    /// AppContext.BaseDirectory. Set to the source Assets folder in dev so the cache lives
    /// in the repo. Leave null in production — cache goes next to the exe.
    /// </summary>
    public string? UntankCacheDirectory { get; set; }

    private string CacheFileFor(string folderPath, string? cacheKey = null)
    {
        if (cacheKey == "untank" && UntankCacheDirectory is not null)
            return Path.Combine(UntankCacheDirectory, $"dslab-cache-{cacheKey}.json");

        if (cacheKey is not null)
            return Path.Combine(AppContext.BaseDirectory, $"dslab-cache-{cacheKey}.json");

        var normalized = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var hash       = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized)))[..12];
        var name       = Path.GetFileName(normalized);
        return Path.Combine(AppContext.BaseDirectory, $"dslab-cache-{name}-{hash}.json");
    }

    public async Task<BitsFolder?> TryLoadAsync(string folderPath, string? cacheKey = null)
    {
        var cachePath = CacheFileFor(folderPath, cacheKey);
        if (!File.Exists(cachePath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(cachePath);
            var dto  = JsonSerializer.Deserialize<FolderDto>(json);
            return dto?.ToModel(parent: null, basePath: folderPath);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(BitsFolder root, string? cacheKey = null)
    {
        var cachePath = CacheFileFor(root.FullPath, cacheKey);
        try
        {
            var dto  = FolderDto.From(root, root.FullPath);
            var json = JsonSerializer.Serialize(dto, JsonOpts);
            await File.WriteAllTextAsync(cachePath, json);
        }
        catch { /* non-critical — next start will re-parse */ }
    }

    // ── Relative-path helpers ─────────────────────────────────────────────

    /// <summary>
    /// Returns a slash-separated path relative to basePath.
    /// Root itself → "". Child → "sub/folder/file.gas".
    /// </summary>
    private static string MakeRelative(string fullPath, string basePath)
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
    private static string MakeAbsolute(string relPath, string basePath)
    {
        var base_ = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (relPath == "") return base_;
        // Check if it's already absolute (fallback path stored when outside base)
        if (Path.IsPathRooted(relPath)) return relPath;
        return base_ + Path.DirectorySeparatorChar + relPath.Replace('/', Path.DirectorySeparatorChar);
    }

    // ── DTOs ─────────────────────────────────────────────────────────────

    private sealed class FolderDto
    {
        public string Name    { get; set; } = "";
        public string RelPath { get; set; } = "";
        public List<FolderDto>   SubFolders { get; set; } = new();
        public List<FileDto>     Files      { get; set; } = new();
        public List<RawFileDto>  RawFiles   { get; set; } = new();

        public static FolderDto From(BitsFolder f, string basePath)
        {
            var dto = new FolderDto { Name = f.Name, RelPath = MakeRelative(f.FullPath, basePath) };
            foreach (var child in f.Children)
            {
                if      (child is BitsFolder sub)  dto.SubFolders.Add(From(sub, basePath));
                else if (child is BitsFile file)   dto.Files.Add(FileDto.From(file, basePath));
                else if (child is BitsRawFile raw)  dto.RawFiles.Add(new RawFileDto { Name = raw.Name, RelPath = MakeRelative(raw.FullPath, basePath) });
            }
            return dto;
        }

        public BitsFolder ToModel(BitsNode? parent, string basePath)
        {
            var folder = new BitsFolder
            {
                Name     = Name,
                FullPath = MakeAbsolute(RelPath, basePath),
                Parent   = parent
            };
            foreach (var sub  in SubFolders) folder.Children.Add(sub.ToModel(folder, basePath));
            foreach (var file in Files)      folder.Children.Add(file.ToModel(folder, basePath));
            foreach (var raw  in RawFiles)   folder.Children.Add(new BitsRawFile { Name = raw.Name, FullPath = MakeAbsolute(raw.RelPath, basePath), Parent = folder });
            return folder;
        }
    }

    private sealed class RawFileDto
    {
        public string Name    { get; set; } = "";
        public string RelPath { get; set; } = "";
    }

    private sealed class FileDto
    {
        public string Name    { get; set; } = "";
        public string RelPath { get; set; } = "";
        public List<TemplateDto> Templates { get; set; } = new();

        public static FileDto From(BitsFile f, string basePath)
        {
            var dto = new FileDto { Name = f.Name, RelPath = MakeRelative(f.FullPath, basePath) };
            foreach (var child in f.Children)
                if (child is BitsTemplate t) dto.Templates.Add(TemplateDto.From(t));
            return dto;
        }

        public BitsFile ToModel(BitsNode parent, string basePath)
        {
            var absPath = MakeAbsolute(RelPath, basePath);
            var file = new BitsFile { Name = Name, FullPath = absPath, Parent = parent };
            foreach (var t in Templates) file.Children.Add(t.ToModel(file, absPath));
            return file;
        }
    }

    private sealed class TemplateDto
    {
        public string Name         { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string SourceCode   { get; set; } = "";

        public static TemplateDto From(BitsTemplate t) => new()
        {
            Name         = t.Name,
            TemplateName = t.TemplateName,
            SourceCode   = t.SourceCode
        };

        // fileFullPath: already-resolved absolute path of the parent .gas file
        public BitsTemplate ToModel(BitsNode parent, string fileFullPath) => new()
        {
            Name         = Name,
            FullPath     = fileFullPath,
            TemplateName = TemplateName,
            SourceCode   = SourceCode,
            Parent       = parent
        };
    }
}
