using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DungeonSiegeLab.Services;

public abstract class AbstractExplorerFolderWatcher : IDisposable
{
    protected readonly Dictionary<string, HashSet<string>> _folderContents = new(StringComparer.OrdinalIgnoreCase);
    protected readonly object _lock = new();
    protected CancellationTokenSource _cts;
    protected bool _isWatching;

    public event Action<string>? FileCreated;
    public event Action<string, string>? FileRenamed;
    public event Action<string>? FileDeleted;

    protected AbstractExplorerFolderWatcher()
    {
        _cts = new CancellationTokenSource();
    }

    public virtual void UpdateWatchedFolders(IEnumerable<string> folderPaths)
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

            var toRemove = _folderContents.Keys.Where(k => !normalized.Contains(k)).ToList();
            foreach (var folder in toRemove)
            {
                _folderContents.Remove(folder);
            }

            foreach (var folder in normalized)
            {
                if (!_folderContents.ContainsKey(folder) && Directory.Exists(folder))
                {
                    _folderContents[folder] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    ScanFolder(folder);
                }
            }
        }
    }

    protected abstract void ScanFolder(string folder);

    protected void OnFileCreated(string fullPath)
    {
        FileCreated?.Invoke(fullPath);
    }

    protected void OnFileRenamed(string oldFullPath, string newFullPath)
    {
        FileRenamed?.Invoke(oldFullPath, newFullPath);
    }

    protected void OnFileDeleted(string fullPath)
    {
        FileDeleted?.Invoke(fullPath);
    }

    public abstract void Dispose();
}

public class WindowsExplorerFolderWatcher : AbstractExplorerFolderWatcher
{
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);

    public WindowsExplorerFolderWatcher() : base()
    {
    }

    public override void UpdateWatchedFolders(IEnumerable<string> folderPaths)
    {
        base.UpdateWatchedFolders(folderPaths);

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

    protected override void ScanFolder(string folder)
    {
        // Not needed for Windows implementation
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
            OnFileCreated(e.FullPath);
    }

    private void OnRenamed(object? sender, RenamedEventArgs e)
    {
        OnFileRenamed(e.OldFullPath, e.FullPath);
    }

    private void OnDeleted(object? sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Deleted)
            OnFileDeleted(e.FullPath);
    }

    private void OnError(object? sender, ErrorEventArgs e)
    {
        Console.WriteLine($"ExplorerFolderWatcherService watcher error: {e.GetException()?.Message}");
    }

    public override void Dispose()
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

public class LinuxExplorerFolderWatcher : AbstractExplorerFolderWatcher
{
    private Thread _pollingThread;

    public LinuxExplorerFolderWatcher() : base()
    {
        _isWatching = false;
    }

    public override void UpdateWatchedFolders(IEnumerable<string> folderPaths)
    {
        base.UpdateWatchedFolders(folderPaths);

        if (!_isWatching)
        {
            _isWatching = true;
            _pollingThread = new Thread(PollForChanges);
            _pollingThread.IsBackground = true;
            _pollingThread.Start();
        }
    }

    protected override void ScanFolder(string folder)
    {
        try
        {
            var contents = WatcherInfoProxy.Instance.GetFolderContents(folder);
            _folderContents[folder] = new HashSet<string>(contents, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning folder {folder}: {ex.Message}");
        }
    }

    private void PollForChanges()
    {
        while (_isWatching && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(1000); // Poll every second

                lock (_lock)
                {
                    foreach (var kvp in _folderContents.ToList())
                    {
                        var folder = kvp.Key;
                        var previousContents = kvp.Value;

                        if (!Directory.Exists(folder))
                        {
                            // Folder deleted
                            _folderContents.Remove(folder);
                            continue;
                        }

                        var currentContents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            foreach (var item in Directory.EnumerateFileSystemEntries(folder))
                            {
                                currentContents.Add(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error enumerating {folder}: {ex.Message}");
                            continue;
                        }

                        // Check for created
                        foreach (var item in currentContents)
                        {
                            if (!previousContents.Contains(item))
                            {
                                OnFileCreated(item);
                            }
                        }

                        // Check for deleted
                        foreach (var item in previousContents)
                        {
                            if (!currentContents.Contains(item))
                            {
                                OnFileDeleted(item);
                            }
                        }

                        // For renames, we can't easily detect, so treat as delete + create
                        // This is a limitation of polling approach

                        _folderContents[folder] = currentContents;
                        WatcherInfoProxy.Instance.UpdateFolderContents(folder, currentContents);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error polling for changes: {ex.Message}");
            }
        }
    }

    public override void Dispose()
    {
        _isWatching = false;
        _cts.Cancel();
        _pollingThread?.Join();
        _cts?.Dispose();
    }
}

public class ExplorerFolderWatcherService : IDisposable
{
    private readonly AbstractExplorerFolderWatcher _watcher;

    public event Action<string>? FileCreated
    {
        add => _watcher.FileCreated += value;
        remove => _watcher.FileCreated -= value;
    }

    public event Action<string, string>? FileRenamed
    {
        add => _watcher.FileRenamed += value;
        remove => _watcher.FileRenamed -= value;
    }

    public event Action<string>? FileDeleted
    {
        add => _watcher.FileDeleted += value;
        remove => _watcher.FileDeleted -= value;
    }

    public ExplorerFolderWatcherService()
    {
        if (OperatingSystem.IsWindows())
        {
            _watcher = new WindowsExplorerFolderWatcher();
        }
        else if (OperatingSystem.IsLinux())
        {
            _watcher = new LinuxExplorerFolderWatcher();
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform for folder watching.");
        }
    }

    public void UpdateWatchedFolders(IEnumerable<string> folderPaths) => _watcher.UpdateWatchedFolders(folderPaths);
    public void Dispose() => _watcher.Dispose();
}
