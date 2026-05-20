using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class ParseResult
    {
        public List<AssignmentRecord> Assignments { get; } = new();
        public List<DependencyReference> NonVanillaBlockDependencies { get; } = new();
        public HashSet<string> LocalSignatures { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Specializes { get; set; }
        public string? AspectModel { get; set; }
        public bool HasExplicitAspectTexture { get; set; }
    }
}
