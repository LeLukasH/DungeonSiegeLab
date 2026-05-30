using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DungeonSiegeLab.Services;

public sealed class WatcherInfoProxy
{
    private static readonly Lazy<WatcherInfoProxy> _instance = new(() => new WatcherInfoProxy());
    private readonly object _lock = new();
    private readonly Dictionary<string, HashSet<string>> _folderContents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, DateTime>> _directoryTimestamps = new(StringComparer.OrdinalIgnoreCase);

    public static WatcherInfoProxy Instance => _instance.Value;

    private WatcherInfoProxy()
    {
    }

    public HashSet<string> GetFolderContents(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        folderPath = Path.GetFullPath(folderPath);

        lock (_lock)
        {
            if (_folderContents.TryGetValue(folderPath, out var contents))
            {
                return new HashSet<string>(contents, StringComparer.OrdinalIgnoreCase);
            }

            var scanned = ScanFolder(folderPath);
            _folderContents[folderPath] = new HashSet<string>(scanned, StringComparer.OrdinalIgnoreCase);
            return new HashSet<string>(scanned, StringComparer.OrdinalIgnoreCase);
        }
    }

    public Dictionary<string, DateTime> GetDirectoryTimestamps(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        directoryPath = Path.GetFullPath(directoryPath);

        lock (_lock)
        {
            if (_directoryTimestamps.TryGetValue(directoryPath, out var timestamps))
            {
                return new Dictionary<string, DateTime>(timestamps, StringComparer.OrdinalIgnoreCase);
            }

            var scanned = ScanDirectory(directoryPath);
            _directoryTimestamps[directoryPath] = new Dictionary<string, DateTime>(scanned, StringComparer.OrdinalIgnoreCase);
            return new Dictionary<string, DateTime>(scanned, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void UpdateFolderContents(string folderPath, IEnumerable<string> contents)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        folderPath = Path.GetFullPath(folderPath);
        lock (_lock)
        {
            _folderContents[folderPath] = new HashSet<string>(contents ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public void UpdateDirectoryTimestamps(string directoryPath, IDictionary<string, DateTime> timestamps)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return;

        directoryPath = Path.GetFullPath(directoryPath);
        lock (_lock)
        {
            _directoryTimestamps[directoryPath] = new Dictionary<string, DateTime>(timestamps ?? new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> ScanFolder(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
                return Array.Empty<string>();

            return Directory.EnumerateFileSystemEntries(folderPath).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static Dictionary<string, DateTime> ScanDirectory(string directoryPath)
    {
        var timestamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!Directory.Exists(directoryPath))
                return timestamps;

            foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                timestamps[file] = File.GetLastWriteTimeUtc(file);
            }
        }
        catch
        {
        }

        return timestamps;
    }
}
