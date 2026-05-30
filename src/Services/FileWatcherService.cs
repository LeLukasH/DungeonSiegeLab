using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace DungeonSiegeLab.Services;

public abstract class AbstractFileWatcher : IDisposable
{
    protected readonly string _directoryPath;
    protected CancellationTokenSource _cts;
    protected bool _isWatching;
    protected readonly Dictionary<string, DateTime> _lastNotificationTimes = new();

    public event Action<string>? FileChanged;

    protected AbstractFileWatcher(string directoryPath)
    {
        _directoryPath = directoryPath;
        _cts = new CancellationTokenSource();
    }

    public void StartWatching()
    {
        if (_isWatching)
        {
            Console.WriteLine("File watcher already running.");
            return;
        }

        Console.WriteLine($"Starting directory watch on: {_directoryPath}");
        _isWatching = true;
        WatchLoop();
    }

    protected abstract void WatchLoop();

    public void StopWatching()
    {
        if (!_isWatching)
        {
            Console.WriteLine("File watcher is not running.");
            return;
        }

        Console.WriteLine($"Stopping directory watch on: {_directoryPath}");
        _isWatching = false;
        _cts.Cancel();
    }

    protected void ProcessFileChange(string fullPath)
    {
        var now = DateTime.Now;
        if (!_lastNotificationTimes.TryGetValue(fullPath, out var lastTime) || (now - lastTime).TotalSeconds > 1)
        {
            _lastNotificationTimes[fullPath] = now;
            Console.WriteLine($"Raising FileChanged event for {fullPath}");
            FileChanged?.Invoke(fullPath);
        }
        else
        {
            Console.WriteLine($"Skipping duplicate notification for {fullPath} (too soon)");
        }
    }

    public void Dispose()
    {
        Console.WriteLine($"Disposing FileWatcher for: {_directoryPath}");
        StopWatching();
        _cts?.Dispose();
    }
}

public class WindowsFileWatcher : AbstractFileWatcher
{
    private IntPtr _directoryHandle;
    private Thread _watcherThread;

    // P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadDirectoryChangesW(
        IntPtr hDirectory,
        IntPtr lpBuffer,
        uint nBufferLength,
        bool bWatchSubtree,
        uint dwNotifyFilter,
        out uint lpBytesReturned,
        IntPtr lpOverlapped,
        IntPtr lpCompletionRoutine);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // Constants
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint FILE_LIST_DIRECTORY = 0x00000001;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_NOTIFY_CHANGE_FILE_NAME = 0x00000001;
    private const uint FILE_NOTIFY_CHANGE_DIR_NAME = 0x00000002;
    private const uint FILE_NOTIFY_CHANGE_ATTRIBUTES = 0x00000004;
    private const uint FILE_NOTIFY_CHANGE_SIZE = 0x00000008;
    private const uint FILE_NOTIFY_CHANGE_LAST_WRITE = 0x00000010;
    private const uint FILE_NOTIFY_CHANGE_LAST_ACCESS = 0x00000020;
    private const uint FILE_NOTIFY_CHANGE_CREATION = 0x00000040;
    private const uint FILE_NOTIFY_CHANGE_SECURITY = 0x00000100;

    // FILE_NOTIFY_INFORMATION structure
    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_NOTIFY_INFORMATION
    {
        public uint NextEntryOffset;
        public uint Action;
        public uint FileNameLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
        public char[] FileName;
    }

    private const uint FILE_ACTION_ADDED = 0x00000001;
    private const uint FILE_ACTION_REMOVED = 0x00000002;
    private const uint FILE_ACTION_MODIFIED = 0x00000003;
    private const uint FILE_ACTION_RENAMED_OLD_NAME = 0x00000004;
    private const uint FILE_ACTION_RENAMED_NEW_NAME = 0x00000005;

    public WindowsFileWatcher(string directoryPath) : base(directoryPath)
    {
    }

    protected override void WatchLoop()
    {
        _directoryHandle = CreateFile(
            _directoryPath,
            FILE_LIST_DIRECTORY,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (_directoryHandle == IntPtr.Zero || _directoryHandle == new IntPtr(-1))
        {
            var err = Marshal.GetLastWin32Error();
            Console.WriteLine($"Failed to open directory handle, error {err}");
            throw new IOException("Failed to open directory handle.");
        }

        _watcherThread = new Thread(WatchDirectory);
        _watcherThread.IsBackground = true;
        _watcherThread.Start();
    }

    private void WatchDirectory()
    {
        const uint bufferSize = 4096;
        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        try
        {
            while (_isWatching && !_cts.Token.IsCancellationRequested)
            {
                uint bytesReturned;
                bool success = ReadDirectoryChangesW(
                    _directoryHandle,
                    buffer,
                    bufferSize,
                    true, // watch subtree
                    FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE_LAST_WRITE | FILE_NOTIFY_CHANGE_SIZE,
                    out bytesReturned,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (!success)
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine($"ReadDirectoryChangesW failed with error {errorCode}");
                    break;
                }

                if (bytesReturned > 0)
                {
                    ProcessNotifications(buffer, bytesReturned);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            if (_directoryHandle != IntPtr.Zero)
            {
                CloseHandle(_directoryHandle);
                _directoryHandle = IntPtr.Zero;
            }
        }
    }

    private void ProcessNotifications(IntPtr buffer, uint bytesReturned)
    {
        uint offset = 0;
        while (offset < bytesReturned)
        {
            var infoPtr = Marshal.PtrToStructure(buffer + (int)offset, typeof(FILE_NOTIFY_INFORMATION));
            if (infoPtr == null) continue;
            FILE_NOTIFY_INFORMATION info = (FILE_NOTIFY_INFORMATION)infoPtr;

            string fileName = new string(info.FileName, 0, (int)info.FileNameLength / 2);
            var fullPath = Path.Combine(_directoryPath, fileName);

            Console.WriteLine($"File change detected in watched directory: {_directoryPath}. Action={info.Action}, FileName={fileName}, FullPath={fullPath}");

            // Check if it's a file change (not directory)
            if ((info.Action == FILE_ACTION_MODIFIED || info.Action == FILE_ACTION_ADDED || info.Action == FILE_ACTION_REMOVED) &&
                !string.IsNullOrEmpty(fileName))
            {
                ProcessFileChange(fullPath);
            }

            if (info.NextEntryOffset == 0)
                break;

            offset += info.NextEntryOffset;
        }
    }
}

public class LinuxFileWatcher : AbstractFileWatcher
{
    private readonly Dictionary<string, DateTime> _fileTimestamps = new();
    private Thread _pollingThread;

    public LinuxFileWatcher(string directoryPath) : base(directoryPath)
    {
    }

    protected override void WatchLoop()
    {
        // Initial scan
        ScanDirectory();

        _pollingThread = new Thread(PollForChanges);
        _pollingThread.IsBackground = true;
        _pollingThread.Start();
    }

    private void ScanDirectory()
    {
        try
        {
            var timestamps = WatcherInfoProxy.Instance.GetDirectoryTimestamps(_directoryPath);
            foreach (var kvp in timestamps)
            {
                _fileTimestamps[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning directory {_directoryPath}: {ex.Message}");
        }
    }

    private void PollForChanges()
    {
        while (_isWatching && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(1000); // Poll every second

                var currentFiles = new HashSet<string>(Directory.EnumerateFiles(_directoryPath, "*", SearchOption.AllDirectories));

                // Check for added or modified files
                foreach (var file in currentFiles)
                {
                    if (_fileTimestamps.TryGetValue(file, out var lastTime))
                    {
                        var currentTime = File.GetLastWriteTimeUtc(file);
                        if (currentTime > lastTime)
                        {
                            _fileTimestamps[file] = currentTime;
                            ProcessFileChange(file);
                        }
                    }
                    else
                    {
                        _fileTimestamps[file] = File.GetLastWriteTimeUtc(file);
                        ProcessFileChange(file);
                    }
                }

                // Check for removed files
                var removedFiles = _fileTimestamps.Keys.Where(f => !currentFiles.Contains(f)).ToList();
                foreach (var file in removedFiles)
                {
                    _fileTimestamps.Remove(file);
                    ProcessFileChange(file);
                }

                WatcherInfoProxy.Instance.UpdateDirectoryTimestamps(_directoryPath, _fileTimestamps);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error polling for changes in {_directoryPath}: {ex.Message}");
            }
        }
    }
}

public class FileWatcherService : IDisposable
{
    private readonly AbstractFileWatcher _watcher;

    public event Action<string>? FileChanged
    {
        add => _watcher.FileChanged += value;
        remove => _watcher.FileChanged -= value;
    }

    public FileWatcherService(string directoryPath)
    {
        if (OperatingSystem.IsWindows())
        {
            _watcher = new WindowsFileWatcher(directoryPath);
        }
        else if (OperatingSystem.IsLinux())
        {
            _watcher = new LinuxFileWatcher(directoryPath);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: use the polling watcher as a reasonable fallback
            _watcher = new LinuxFileWatcher(directoryPath);
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported platform for file watching.");
        }
    }

    public void StartWatching() => _watcher.StartWatching();
    public void StopWatching() => _watcher.StopWatching();
    public void Dispose() => _watcher.Dispose();
}