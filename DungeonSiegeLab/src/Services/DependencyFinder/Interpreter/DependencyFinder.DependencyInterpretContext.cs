using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class DependencyInterpretContext
    {
        public required DependencyFinder Finder { get; init; }
        public required string TemplateName { get; init; }
        public required List<DependencyReference> Dependencies { get; init; }
        public required AssignmentRecord Assignment { get; init; }
    }
}
