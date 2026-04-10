using System.Text.Json;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Persists the tree expansion state and last loaded Bits path to a JSON file
/// stored next to the executable.
/// </summary>
public class TreeStateService
{
    private static readonly string StateFilePath =
        Path.Combine(AppContext.BaseDirectory, "dslab-state.json");

    private AppState _state = new();

    public string? LastBitsPath => _state.LastBitsPath;

    public void Load()
    {
        if (!File.Exists(StateFilePath)) return;
        try
        {
            var json = File.ReadAllText(StateFilePath);
            _state = JsonSerializer.Deserialize<AppState>(json) ?? new();
        }
        catch
        {
            _state = new();
        }
    }

    public HashSet<string> GetExpandedPaths(string bitsPath)
    {
        if (_state.ExpandedPaths.TryGetValue(bitsPath, out var paths))
            return new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void SaveExpansionState(string bitsPath, IEnumerable<string> expandedPaths)
    {
        _state.LastBitsPath = bitsPath;
        _state.ExpandedPaths[bitsPath] = expandedPaths.ToList();
        WriteFile();
    }

    private void WriteFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json);
        }
        catch { /* ignore write errors */ }
    }

    private sealed class AppState
    {
        public string? LastBitsPath { get; set; }
        public Dictionary<string, List<string>> ExpandedPaths { get; set; } = new();
    }
}
