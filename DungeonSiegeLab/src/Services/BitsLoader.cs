using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Načíta /Bits priečinok a vytvorí hierarchickú stromovú štruktúru
/// BitsNode objektov (Composite pattern).
/// </summary>
public class BitsLoader
{
    private readonly GasParser _gasParser = new();

    public async Task<BitsFolder> LoadAsync(string bitsPath)
    {
        if (!Directory.Exists(bitsPath))
            throw new DirectoryNotFoundException($"Priečinok neexistuje: {bitsPath}");

        var root = new BitsFolder
        {
            Name = Path.GetFileName(bitsPath.TrimEnd(Path.DirectorySeparatorChar)),
            FullPath = bitsPath
        };

        await PopulateFolderAsync(root, bitsPath);
        return root;
    }

    private async Task PopulateFolderAsync(BitsFolder folder, string path)
    {
        // Podpriečinky
        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
        {
            var subFolder = new BitsFolder
            {
                Name = Path.GetFileName(dir),
                FullPath = dir,
                Parent = folder
            };

            await PopulateFolderAsync(subFolder, dir);
            folder.Children.Add(subFolder);
        }

        // .gas súbory
        foreach (var gasFile in Directory.GetFiles(path, "*.gas").OrderBy(f => f))
        {
            var fileNode = new BitsFile
            {
                Name = Path.GetFileName(gasFile),
                FullPath = gasFile,
                Parent = folder
            };

            try
            {
                var templates = await _gasParser.ParseFileAsync(gasFile);
                foreach (var template in templates)
                {
                    template.Parent = fileNode;
                    fileNode.Children.Add(template);
                }
            }
            catch
            {
                // Ak sa súbor nedá parsovať, pridaj ho prázdny
            }

            folder.Children.Add(fileNode);
        }
    }
}
