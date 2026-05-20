using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public interface IBitsLoader
{
    Task<BitsFolder> LoadAsync(string path, IProgress<(int percent, string folder)>? progress = null);
}
