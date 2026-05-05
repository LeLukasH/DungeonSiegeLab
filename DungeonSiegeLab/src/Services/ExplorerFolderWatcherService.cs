using System;
using System.Collections.Generic;
using System.IO;

namespace DungeonSiegeLab.Services;

public class ExplorerFolderWatcherService : IDisposable
{
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public event Action<string>? FileCreated;
    public event Action<string, string>? FileRenamed;
    public event Action<string>? FileDeleted;

    public void UpdateWatchedFolders(IEnumerable<string> folderPaths)
    {
        lock (_lock)
        {
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folderPaths)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;
                try
                {
                    var path = Path.GetFullPath(folder);
                    normalized.Add(path);
                }
                catch
                {
                    // ignore invalid paths
                }
            }

            foreach (var existing in new List<string>(_watchers.Keys))
            {
                if (!normalized.Contains(existing))
                {
                    RemoveWatcher(existing);
                }
            }

            foreach (var folder in normalized)
            {
                if (!_watchers.ContainsKey(folder) && Directory.Exists(folder))
                    AddWatcher(folder);
            }
        }
    }

    private void AddWatcher(string folder)
    {
        try
        {
            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };

            watcher.Created += OnCreated;
            watcher.Renamed += OnRenamed;
            watcher.Deleted += OnDeleted;
            watcher.Error += OnError;

            _watchers[folder] = watcher;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ExplorerFolderWatcherService failed to watch {folder}: {ex.Message}");
        }
    }

    private void RemoveWatcher(string folder)
    {
        if (_watchers.TryGetValue(folder, out var watcher))
        {
            watcher.Created -= OnCreated;
            watcher.Renamed -= OnRenamed;
            watcher.Deleted -= OnDeleted;
            watcher.Error -= OnError;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _watchers.Remove(folder);
        }
    }

    private void OnCreated(object? sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Created)
            FileCreated?.Invoke(e.FullPath);
    }

    private void OnRenamed(object? sender, RenamedEventArgs e)
    {
        FileRenamed?.Invoke(e.OldFullPath, e.FullPath);
    }

    private void OnDeleted(object? sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Deleted)
            FileDeleted?.Invoke(e.FullPath);
    }

    private void OnError(object? sender, ErrorEventArgs e)
    {
        Console.WriteLine($"ExplorerFolderWatcherService watcher error: {e.GetException()?.Message}");
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.Created -= OnCreated;
                watcher.Renamed -= OnRenamed;
                watcher.Deleted -= OnDeleted;
                watcher.Error -= OnError;
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }
}
