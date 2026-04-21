using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using DungeonSiegeLab.Models;
using System.IO;
using Avalonia.Threading;
using DungeonSiegeLab.Services;
using System.Timers;

namespace DungeonSiegeLab.ViewModels;

/// <summary>
/// Represents one open template in the code viewer.
/// IsPreview = true → italic tab title, replaced on next single-click (VSCode preview behavior).
/// IsPreview = false → permanent tab, stays until explicitly closed.
/// </summary>
public partial class CodeTabViewModel : ViewModelBase
{
    public BitsNodeViewModel Node { get; }

    [ObservableProperty] private bool _isPreview;
    // preparation for dependency implementation 
    [ObservableProperty] private ObservableCollection<DependencyReference> _dependencies = new();
    [ObservableProperty] private DependencyReference? _selectedDependency;
    [ObservableProperty] private bool _isDependencyPopupOpen;

    [ObservableProperty] private bool _isStatusExpanded;

    public string Name => Node.Name;

    [ObservableProperty] private string _sourceCode = "";

    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _reloadTimer;
    private string? _watchedPath;
    private DateTime _lastKnownWriteTime = DateTime.MinValue;

    /// <summary>Italic when preview, normal when permanent — bound directly in XAML.</summary>
    public FontStyle TabFontStyle => IsPreview ? FontStyle.Italic : FontStyle.Normal;

    partial void OnIsPreviewChanged(bool value) => OnPropertyChanged(nameof(TabFontStyle));

    public CodeTabViewModel(BitsNodeViewModel node, bool isPreview)
    {
        Node = node;
        _isPreview = isPreview;

        if (node.Node is BitsTemplate t)
        {
            _sourceCode = t.SourceCode;
        }
        else if (node.IsRawFile || node.IsEmptyFile)
        {
            _ = LoadRawContentAsync();
        }

        // Start watching the file for changes
        string fileToWatch = Node.FullPath;
        if (Node.Node is BitsTemplate template)
            fileToWatch = template.Parent.FullPath;

        if (File.Exists(fileToWatch))
        {
            _watchedPath = fileToWatch;
            UpdateLastWriteTime();

            var dir = Path.GetDirectoryName(fileToWatch);
            var fileName = Path.GetFileName(fileToWatch);
            _watcher = new FileSystemWatcher(dir, fileName);
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.EnableRaisingEvents = true;

            _reloadTimer = new System.Timers.Timer(500); // 500ms debounce
            _reloadTimer.Elapsed += OnReloadTimerElapsed;
            _reloadTimer.AutoReset = false;
        }

        if (node.Node is BitsTemplate templateNode)
        {
            _ = EnsureTemplateIsFreshAsync(templateNode);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_reloadTimer != null)
        {
            _reloadTimer.Stop();
            _reloadTimer.Start();
        }
    }

    private void OnReloadTimerElapsed(object sender, ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await ReloadContentAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reloading content for {Node.FullPath}: {ex.Message}");
            }
        });
    }

    public async Task ReloadContentAsync()
    {
        try
        {
            if (Node.Node is BitsTemplate t)
            {
                var gasParser = new GasParser();
                var templates = await gasParser.ParseFileAsync(t.Parent.FullPath);
                var updatedTemplate = templates.FirstOrDefault(temp => temp.TemplateName == t.TemplateName);
                if (updatedTemplate != null)
                {
                    SourceCode = updatedTemplate.SourceCode;
                    UpdateLastWriteTime();
                }
            }
            else if (Node.IsRawFile || Node.IsEmptyFile)
            {
                await LoadRawContentAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ReloadContentAsync for {Node.FullPath}: {ex.Message}");
        }
    }

    private async Task EnsureTemplateIsFreshAsync(BitsTemplate t)
    {
        try
        {
            if (string.IsNullOrEmpty(_watchedPath) || !File.Exists(_watchedPath))
                return;

            var templates = await new GasParser().ParseFileAsync(_watchedPath);
            var updatedTemplate = templates.FirstOrDefault(temp => temp.TemplateName == t.TemplateName);
            if (updatedTemplate != null && updatedTemplate.SourceCode != SourceCode)
            {
                SourceCode = updatedTemplate.SourceCode;
                UpdateLastWriteTime();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking template freshness for {Node.FullPath}: {ex.Message}");
        }
    }

    public bool HasExternalModifications()
    {
        if (string.IsNullOrEmpty(_watchedPath))
            return false;
        if (!File.Exists(_watchedPath))
            return false;

        return File.GetLastWriteTimeUtc(_watchedPath) > _lastKnownWriteTime;
    }

    public async Task ReloadIfChangedAsync()
    {
        if (HasExternalModifications())
            await ReloadContentAsync();
    }

    private void UpdateLastWriteTime()
    {
        if (string.IsNullOrEmpty(_watchedPath) || !File.Exists(_watchedPath))
            return;

        _lastKnownWriteTime = File.GetLastWriteTimeUtc(_watchedPath);
    }

    private async Task LoadRawContentAsync()
    {
        try
        {
            var info = new FileInfo(Node.FullPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                SourceCode = $"// File too large to display ({info.Length / 1024:N0} KB)";
                return;
            }

            var bytes = await File.ReadAllBytesAsync(Node.FullPath);

            // Binary detection: null byte in first 8 KB
            int check = Math.Min(bytes.Length, 8192);
            for (int i = 0; i < check; i++)
            {
                if (bytes[i] == 0)
                {
                    SourceCode = "// Binary file — cannot display content";
                    return;
                }
            }

            SourceCode = System.Text.Encoding.UTF8.GetString(bytes);
            UpdateLastWriteTime();
        }
        catch (Exception ex)
        {
            SourceCode = $"// Could not read file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleStatus()
    {
        Console.WriteLine("Toggle clicked");
        IsStatusExpanded = !IsStatusExpanded;
    }

    [RelayCommand]
    private void OpenDependency(DependencyReference dep)
    {
        SelectedDependency = dep;
        IsDependencyPopupOpen = true;
    }

    [RelayCommand]
    private void CloseDependency()
    {
        IsDependencyPopupOpen = false;
        SelectedDependency = null;
    }
}
