using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

internal class DiskBitsLoader : IBitsLoader
{
    private readonly GasParser _gasParser = new();

    public async Task<BitsFolder> LoadAsync(string bitsPath, IProgress<(int percent, string folder)>? progress = null)
    {
        if (!Directory.Exists(bitsPath))
            throw new DirectoryNotFoundException($"Folder does not exist: {bitsPath}");

        var root = new BitsFolder
        {
            Name     = Path.GetFileName(bitsPath.TrimEnd(Path.DirectorySeparatorChar)),
            FullPath = bitsPath
        };

        await Task.Run(async () =>
        {
            var total   = Directory.GetDirectories(bitsPath, "*", SearchOption.AllDirectories).Length + 1;
            var counter = new[] { 0 };
            await PopulateFolderAsync(root, bitsPath, progress, counter, total);
        });

        return root;
    }

    private async Task PopulateFolderAsync(
        BitsFolder folder, string path,
        IProgress<(int percent, string folder)>? progress,
        int[] counter, int total)
    {
        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
        {
            var subFolder = new BitsFolder
            {
                Name     = Path.GetFileName(dir),
                FullPath = dir,
                Parent   = folder
            };
            await PopulateFolderAsync(subFolder, dir, progress, counter, total);
            folder.Children.Add(subFolder);
        }

        var allFiles = Directory.GetFiles(path).OrderBy(f => f).ToArray();

        var gasFiles = allFiles.Where(f => f.EndsWith(".gas", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (gasFiles.Length > 0)
        {
            var fileNodeTasks = gasFiles.Select(async gasFile =>
            {
                var fileNode = new BitsFile
                {
                    Name     = Path.GetFileName(gasFile),
                    FullPath = gasFile,
                    Parent   = folder
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
                catch { }
                return fileNode;
            }).ToArray();

            foreach (var fileNode in await Task.WhenAll(fileNodeTasks))
                folder.Children.Add(fileNode);
        }

        foreach (var file in allFiles.Where(f => !f.EndsWith(".gas", StringComparison.OrdinalIgnoreCase)))
        {
            folder.Children.Add(new BitsRawFile
            {
                Name     = Path.GetFileName(file),
                FullPath = file,
                Parent   = folder
            });
        }

        counter[0]++;
        progress?.Report((counter[0] * 100 / total, path));
    }
}
