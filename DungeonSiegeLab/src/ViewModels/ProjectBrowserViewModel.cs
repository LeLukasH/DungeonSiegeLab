using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

public partial class ProjectBrowserViewModel : ViewModelBase
{
    private readonly BitsLoader _bitsLoader = new();
    private readonly TextureFinder _textureFinder = new();
    private readonly TreeStateService _treeState = new();
    private CancellationTokenSource? _searchCts;

    /// <summary>Bundled base-game data folder, placed next to the executable.</summary>
    public static readonly string UntankPath =
        Path.Combine(AppContext.BaseDirectory, "Untank");

    [ObservableProperty] private ObservableCollection<BitsNodeViewModel> _rootNodes = new();
    [ObservableProperty] private BitsNodeViewModel? _selectedNode;
    [ObservableProperty] private string _statusMessage = "Load a /Bits folder.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _bitsPath = "";

    [ObservableProperty] private ObservableCollection<CodeTabViewModel> _openCodeTabs = new();
    [ObservableProperty] private CodeTabViewModel? _selectedCodeTab;

    public bool HasOpenCodeTabs => OpenCodeTabs.Count > 0;
    public bool HasNoOpenCodeTabs => OpenCodeTabs.Count == 0;

    // ─── Search panel ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSearchPanelActive;
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _searchInNames = true;
    [ObservableProperty] private bool _searchInContent = true;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _searchStatusMessage = "";
    [ObservableProperty] private ObservableCollection<SearchResultViewModel> _searchResults = new();
    [ObservableProperty] private SearchResultViewModel? _selectedSearchResult;

    private string _bitsRootPath = "";
    private BitsNodeViewModel? _bitsRootVm;
    private BitsNodeViewModel? _untankRootVm;
    private CancellationTokenSource? _saveCts;

    public event Action<List<TextureReference>>? TexturesIdentified;

    public ProjectBrowserViewModel()
    {
        _treeState.Load();
        BitsPath = _treeState.LastBitsPath ?? "";
        BitsNodeViewModel.AnyExpansionChanged += SaveExpansionStateDebounced;
        OpenCodeTabs.CollectionChanged += OnOpenCodeTabsChanged;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Load user's Bits folder first (with expansion restored), then Untank
        if (!string.IsNullOrEmpty(BitsPath))
            await LoadCoreAsync(BitsPath, restoreExpansion: true);
        await LoadUntankAsync(restoreExpansion: true);
    }

    private void OnOpenCodeTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasOpenCodeTabs));
        OnPropertyChanged(nameof(HasNoOpenCodeTabs));
    }

    // ─── Load user's Bits folder ──────────────────────────────────────────

    [RelayCommand]
    private Task LoadBitsFolderAsync(string path) => LoadCoreAsync(path, restoreExpansion: false);

    [RelayCommand]
    private async Task BrowseForBitsFolderAsync(IStorageProvider? storageProvider)
    {
        if (storageProvider is null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select /Bits folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        BitsPath = folders[0].Path.LocalPath;
        await LoadCoreAsync(BitsPath, restoreExpansion: false);
    }

    private async Task LoadCoreAsync(string path, bool restoreExpansion)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        IsLoading = true;
        StatusMessage = "Loading...";
        OpenCodeTabs.Clear();
        SelectedCodeTab = null;

        // Rebuild root list: clear old Bits root, keep Untank
        _bitsRootVm = null;
        RootNodes.Clear();
        if (_untankRootVm != null)
            RootNodes.Add(_untankRootVm);

        try
        {
            _bitsRootPath = path;
            var root = await _bitsLoader.LoadAsync(path);
            _bitsRootVm = new BitsNodeViewModel(root);

            // Bits goes first — insert before Untank
            RootNodes.Insert(0, _bitsRootVm);

            if (restoreExpansion)
            {
                var saved = _treeState.GetExpandedPaths(path);
                if (saved.Count > 0)
                {
                    BitsNodeViewModel.AnyExpansionChanged -= SaveExpansionStateDebounced;
                    RestoreExpansionState(new[] { _bitsRootVm }, saved);
                    BitsNodeViewModel.AnyExpansionChanged += SaveExpansionStateDebounced;
                }
            }

            _treeState.SaveExpansionState(_bitsRootPath, CollectExpandedPaths(new[] { _bitsRootVm }));
            StatusMessage = $"Loaded: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Load bundled Untank folder ───────────────────────────────────────

    private async Task LoadUntankAsync(bool restoreExpansion = false)
    {
        if (!Directory.Exists(UntankPath)) return;

        try
        {
            var root = await _bitsLoader.LoadAsync(UntankPath);
            _untankRootVm = new BitsNodeViewModel(root);

            if (restoreExpansion)
            {
                var saved = _treeState.GetExpandedPaths(UntankPath);
                if (saved.Count > 0)
                {
                    BitsNodeViewModel.AnyExpansionChanged -= SaveExpansionStateDebounced;
                    RestoreExpansionState(new[] { _untankRootVm }, saved);
                    BitsNodeViewModel.AnyExpansionChanged += SaveExpansionStateDebounced;
                }
            }

            // Untank always goes last
            RootNodes.Add(_untankRootVm);
        }
        catch { /* Untank load failure is non-critical */ }
    }

    // ─── Expansion state ──────────────────────────────────────────────────

    private static void RestoreExpansionState(IEnumerable<BitsNodeViewModel> roots, HashSet<string> expandedPaths)
    {
        foreach (var node in roots)
        {
            node.IsExpanded = expandedPaths.Contains(node.FullPath);
            if (node.Children.Count > 0)
                RestoreExpansionState(node.Children, expandedPaths);
        }
    }

    private void SaveExpansionStateDebounced()
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        DelayedSave(_saveCts.Token);
    }

    private async void DelayedSave(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);

            if (!string.IsNullOrEmpty(_bitsRootPath) && _bitsRootVm != null)
                _treeState.SaveExpansionState(_bitsRootPath, CollectExpandedPaths(new[] { _bitsRootVm }));

            if (_untankRootVm != null)
                _treeState.SaveExpansionOnly(UntankPath, CollectExpandedPaths(new[] { _untankRootVm }));
        }
        catch (OperationCanceledException) { }
    }

    private static List<string> CollectExpandedPaths(IEnumerable<BitsNodeViewModel> roots)
    {
        var result = new List<string>();
        foreach (var node in roots)
        {
            if (node.IsExpanded) result.Add(node.FullPath);
            if (node.Children.Count > 0) result.AddRange(CollectExpandedPaths(node.Children));
        }
        return result;
    }

    // ─── Tree selection (single click → preview tab) ──────────────────────

    partial void OnSelectedNodeChanged(BitsNodeViewModel? value)
    {
        if (value?.Node is BitsTemplate template)
        {
            OpenPreviewTab(value);
            StatusMessage = $"Template: {template.TemplateName}";
        }
        else if (value?.Node is BitsFile file)
        {
            StatusMessage = $"File: {file.Name}";
        }
    }

    public void OpenPreviewTab(BitsNodeViewModel node)
    {
        var permanent = OpenCodeTabs.FirstOrDefault(t => !t.IsPreview && t.Node == node);
        if (permanent != null) { SelectedCodeTab = permanent; return; }

        var currentPreview = OpenCodeTabs.FirstOrDefault(t => t.IsPreview);
        if (currentPreview?.Node == node) return;

        if (currentPreview != null) OpenCodeTabs.Remove(currentPreview);

        var tab = new CodeTabViewModel(node, isPreview: true);
        OpenCodeTabs.Add(tab);
        SelectedCodeTab = tab;
    }

    public void PromoteToPermanent(BitsNodeViewModel node)
    {
        if (node.Node is not BitsTemplate) return;

        var existing = OpenCodeTabs.FirstOrDefault(t => t.Node == node);
        if (existing != null)
        {
            existing.IsPreview = false;
            SelectedCodeTab = existing;
            return;
        }

        var preview = OpenCodeTabs.FirstOrDefault(t => t.IsPreview);
        if (preview != null) OpenCodeTabs.Remove(preview);

        var tab = new CodeTabViewModel(node, isPreview: false);
        OpenCodeTabs.Add(tab);
        SelectedCodeTab = tab;
    }

    [RelayCommand]
    private void CloseCodeTab(CodeTabViewModel? tab)
    {
        if (tab is null) return;
        var idx = OpenCodeTabs.IndexOf(tab);
        OpenCodeTabs.Remove(tab);
        if (SelectedCodeTab == tab)
            SelectedCodeTab = idx > 0 ? OpenCodeTabs[idx - 1] : OpenCodeTabs.FirstOrDefault();
    }

    // ─── Identify Textures ────────────────────────────────────────────────

    [RelayCommand]
    private void IdentifyTextures()
    {
        if (SelectedNode?.Node is not BitsTemplate template)
        {
            StatusMessage = "Select a template first.";
            return;
        }

        var textures = _textureFinder.FindInTemplate(template);

        // User's Bits folder has priority; Untank fills in anything not found there
        if (!string.IsNullOrEmpty(_bitsRootPath))
            _textureFinder.ResolveTextureFiles(textures, _bitsRootPath);
        _textureFinder.ResolveTextureFiles(textures, UntankPath, overwriteExisting: false);

        StatusMessage = $"Found {textures.Count} texture(s) in '{template.TemplateName}'.";
        TexturesIdentified?.Invoke(textures);
    }

    // ─── Search ───────────────────────────────────────────────────────────

    [RelayCommand] private void ShowExplorer() => IsSearchPanelActive = false;
    [RelayCommand] private void ShowSearch()   => IsSearchPanelActive = true;

    partial void OnSearchQueryChanged(string value)      => TriggerSearchDebounced();
    partial void OnSearchInNamesChanged(bool value)      => TriggerSearchDebounced();
    partial void OnSearchInContentChanged(bool value)    => TriggerSearchDebounced();

    partial void OnSelectedSearchResultChanged(SearchResultViewModel? value)
    {
        if (value is null) return;
        OpenPreviewTab(value.Node);
    }

    private void TriggerSearchDebounced()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        _ = RunSearchAsync(_searchCts.Token);
    }

    private async Task RunSearchAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            SearchStatusMessage = "";
            IsSearching = false;
            return;
        }

        IsSearching = true;
        SearchStatusMessage = "Searching…";

        try
        {
            await Task.Delay(300, token);

            // Snapshot all state on the UI thread before going to background
            var roots     = RootNodes.ToList();
            var query     = SearchQuery;
            var inNames   = SearchInNames;
            var inContent = SearchInContent;

            var results = await Task.Run(
                () => DoSearch(roots, query, inNames, inContent), token);

            SearchResults.Clear();
            foreach (var r in results) SearchResults.Add(r);

            SearchStatusMessage = results.Count == 0
                ? "No results."
                : $"{results.Count} result{(results.Count == 1 ? "" : "s")}";
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!token.IsCancellationRequested)
                IsSearching = false;
        }
    }

    private static List<SearchResultViewModel> DoSearch(
        List<BitsNodeViewModel> roots, string query, bool inNames, bool inContent)
    {
        var results = new List<SearchResultViewModel>();
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
            SearchNode(root, root.FullPath, query, inNames, inContent, results, seen);
        return results;
    }

    private static void SearchNode(
        BitsNodeViewModel node, string rootPath,
        string query, bool inNames, bool inContent,
        List<SearchResultViewModel> results, HashSet<string> seen)
    {
        if (node.IsTemplate && node.Node is Models.BitsTemplate template)
        {
            bool nameMatch = inNames &&
                node.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

            string snippet = "";
            bool contentMatch = false;
            if (inContent)
            {
                var line = FindMatchingLine(template.SourceCode, query);
                if (line is not null) { contentMatch = true; snippet = line; }
            }

            if ((nameMatch || contentMatch) && seen.Add(node.FullPath))
            {
                var rel = node.FullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
                    ? node.FullPath[rootPath.Length..].TrimStart('/', '\\')
                    : node.FullPath;
                results.Add(new SearchResultViewModel(node, rel, snippet));
            }
        }

        foreach (var child in node.Children)
            SearchNode(child, rootPath, query, inNames, inContent, results, seen);
    }

    private static string? FindMatchingLine(string sourceCode, string query)
    {
        foreach (var rawLine in sourceCode.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                return line.Length > 80 ? line[..80] + "…" : line;
        }
        return null;
    }

    // ─── Identify Dependencies ────────────────────────────────────────────

    [RelayCommand]
    private void IdentifyDependencies()
    {
        if (SelectedNode?.Node is not BitsTemplate template)
        {
            StatusMessage = "Select a template first.";
            return;
        }

        var specializesRegex = new System.Text.RegularExpressions.Regex(
            @"\bspecializes\s*=\s*(\w+)\s*;", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var matches = specializesRegex.Matches(template.SourceCode);
        if (matches.Count == 0)
        {
            StatusMessage = $"'{template.TemplateName}' has no dependencies (specializes).";
            return;
        }

        var deps = string.Join(", ", matches.Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value));
        StatusMessage = $"Dependencies of '{template.TemplateName}': {deps}";
    }
}
