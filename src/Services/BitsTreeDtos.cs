using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

internal sealed class FolderDto
{
    public string Name    { get; set; } = "";
    public string RelPath { get; set; } = "";
    public List<FolderDto>  SubFolders { get; set; } = new();
    public List<FileDto>    Files      { get; set; } = new();
    public List<RawFileDto> RawFiles   { get; set; } = new();

    public static FolderDto From(BitsFolder f, string basePath)
    {
        var dto = new FolderDto { Name = f.Name, RelPath = TreeCacheService.MakeRelative(f.FullPath, basePath) };
        foreach (var child in f.Children)
            child.AddToFolderDto(dto, basePath);
        return dto;
    }

    public BitsFolder ToModel(BitsComponent? parent, string basePath)
    {
        var folder = new BitsFolder
        {
            Name     = Name,
            FullPath = TreeCacheService.MakeAbsolute(RelPath, basePath),
            Parent   = parent
        };
        foreach (var sub  in SubFolders) folder.Children.Add(sub.ToModel(folder, basePath));
        foreach (var file in Files)      folder.Children.Add(file.ToModel(folder, basePath));
        foreach (var raw  in RawFiles)   folder.Children.Add(new BitsRawFile { Name = raw.Name, FullPath = TreeCacheService.MakeAbsolute(raw.RelPath, basePath), Parent = folder });
        return folder;
    }
}

internal sealed class RawFileDto
{
    public string Name    { get; set; } = "";
    public string RelPath { get; set; } = "";

    public static RawFileDto From(BitsRawFile r, string basePath)
        => new() { Name = r.Name, RelPath = TreeCacheService.MakeRelative(r.FullPath, basePath) };
}

internal sealed class FileDto
{
    public string Name    { get; set; } = "";
    public string RelPath { get; set; } = "";
    public List<TemplateDto> Templates { get; set; } = new();

    public static FileDto From(BitsFile f, string basePath)
    {
        var dto = new FileDto { Name = f.Name, RelPath = TreeCacheService.MakeRelative(f.FullPath, basePath) };
        foreach (var child in f.Children)
            child.AddToFileDto(dto);
        return dto;
    }

    public BitsFile ToModel(BitsComponent parent, string basePath)
    {
        var absPath = TreeCacheService.MakeAbsolute(RelPath, basePath);
        var file = new BitsFile { Name = Name, FullPath = absPath, Parent = parent };
        foreach (var t in Templates) file.Children.Add(t.ToModel(file, absPath));
        return file;
    }
}

internal sealed class TemplateDto
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

    public BitsTemplate ToModel(BitsComponent parent, string fileFullPath) => new()
    {
        Name         = Name,
        FullPath     = fileFullPath,
        TemplateName = TemplateName,
        SourceCode   = SourceCode,
        Parent       = parent
    };
}
