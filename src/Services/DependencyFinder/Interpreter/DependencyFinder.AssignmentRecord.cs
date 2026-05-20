namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class AssignmentRecord
    {
        public string Path { get; init; } = "";
        public string Key { get; init; } = "";
        public string Value { get; init; } = "";
        public int Line { get; init; }
        public string Signature { get; init; } = "";
    }
}
