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

        public BitsFolder ToModel(BitsComponent? parent, string basePath)
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

        public BitsFile ToModel(BitsComponent parent, string basePath)
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
        public BitsTemplate ToModel(BitsComponent parent, string fileFullPath) => new()
        {
            Name         = Name,
            FullPath     = fileFullPath,
            TemplateName = TemplateName,
            SourceCode   = SourceCode,
            Parent       = parent
        };
    }
}
