using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class AnalyzeResult
    {
        public List<DependencyReference> Dependencies { get; } = new();
        public HashSet<string> LocalSignatures { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
