using System.Text.Json;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Persists the tree expansion state and last loaded Bits path to a JSON file
/// stored next to the executable.
/// </summary>
public class TreeStateService
{
    public static readonly TreeStateService Instance = new();

    private static readonly string StateFilePath =
        Path.Combine(AppContext.BaseDirectory, "dslab-state.json");

    private AppState _state = new();

    private const int MaxRecentPaths = 10;

    public string? LastBitsPath => _state.LastBitsPath;

    public IReadOnlyList<string> RecentPaths => _state.RecentBitsPaths;

    public void AddRecentPath(string path)
    {
        _state.RecentBitsPaths.Remove(path);
        _state.RecentBitsPaths.Insert(0, path);
        if (_state.RecentBitsPaths.Count > MaxRecentPaths)
            _state.RecentBitsPaths.RemoveAt(_state.RecentBitsPaths.Count - 1);
        WriteFile();
    }

    public bool CollapseSubfoldersRecursively
    {
        get => _state.CollapseSubfoldersRecursively;
        set { _state.CollapseSubfoldersRecursively = value; WriteFile(); }
    }

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

    /// <summary>Saves expansion state AND updates LastBitsPath (use for the user's Bits folder).</summary>
    public void SaveExpansionState(string bitsPath, IEnumerable<string> expandedPaths)
    {
        _state.LastBitsPath = bitsPath;
        _state.ExpandedPaths[bitsPath] = expandedPaths.ToList();
        WriteFile();
    }

    /// <summary>Saves expansion state for a secondary root (e.g. Untank) without touching LastBitsPath.</summary>
    public void SaveExpansionOnly(string key, IEnumerable<string> expandedPaths)
    {
        _state.ExpandedPaths[key] = expandedPaths.ToList();
        WriteFile();
    }

    /// <summary>Removes the expansion state for a path that is no longer loaded.</summary>
    public void RemoveExpandedPaths(string key)
    {
        if (_state.ExpandedPaths.Remove(key))
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
        public bool CollapseSubfoldersRecursively { get; set; } = false;
        public List<string> RecentBitsPaths { get; set; } = new();
    }
}
