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
    private readonly DependencyFinder _dependencyFinder = new();
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
    [ObservableProperty] private ObservableCollection<DependencyReference> _identifiedDependencies = new();
    [ObservableProperty] private DependencyReference? _selectedIdentifiedDependency;

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
    public event Action<List<DependencyReference>>? DependenciesIdentified;

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

    // ─── Identify ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void Identify()
    {
        if (SelectedNode?.Node is not BitsTemplate template)
        {
            StatusMessage = "Select a template first.";
            return;
        }

        // Run unified dependency analysis (local + inherited) for selected template.
        var dependencies = _dependencyFinder.IdentifyDependencies(template, BuildTemplateIndex());

        if (dependencies.Count == 0)
        {
            IdentifiedDependencies.Clear();
            StatusMessage = $"'{template.TemplateName}' has no dependencies.";
            return;
        }

        // Adam integration point:
        // 1) IdentifiedDependencies = latest whole result set (global panel / sidebar source)
        // 2) CodeTab.Dependencies = per-tab source list
        // 3) DependencyReference.SourcePath + Line = source navigation/highlight anchors
        // 4) DependencyReference.SourceTemplate + IsInherited = origin/inheritance badges
        UpdateIdentifiedDependencies(dependencies);
        PushDependenciesToCodeTab(template, dependencies);
        DependenciesIdentified?.Invoke(dependencies);

        // Texture Lab still uses TextureReference, so map texture dependencies to that model.
        var textures = dependencies
            .Where(d => d.Kind == DependencyKind.Texture)
            .Select(d => new TextureReference
            {
                TextureName = d.Value,
                Source = d.Rule switch
                {
                    "aspect:textures" => TextureSourceType.AspectTextures,
                    "aspect:model:implicit" => TextureSourceType.AspectModel,
                    "fixed:gui:inventory_icon" => TextureSourceType.InventoryIcon,
                    _ => TextureSourceType.ComponentAttribute
                },
                AttributePath = d.SourcePath
            })
            .GroupBy(t => t.TextureName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (!string.IsNullOrEmpty(_bitsRootPath))
            _textureFinder.ResolveTextureFiles(textures, _bitsRootPath);
        _textureFinder.ResolveTextureFiles(textures, UntankPath, overwriteExisting: false);

        // Keeps backward compatibility: event emits resolved textures for consumers that need them.
        if (textures.Count > 0)
            TexturesIdentified?.Invoke(textures);

        var localCount = dependencies.Count(d => !d.IsInherited);
        var inheritedCount = dependencies.Count(d => d.IsInherited);

        // Build compact diagnostics for quick parser validation in the status bar.
        var byType = dependencies
            .GroupBy(d => d.Kind)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var local = g.Count(d => !d.IsInherited);
                var inherited = g.Count(d => d.IsInherited);
                return $"{g.Key}:{local}L/{inherited}I";
            });

        StatusMessage =
            $"'{template.TemplateName}' → Local:{localCount} | Inherited:{inheritedCount} | {string.Join(" | ", byType)}";
    }

    private void UpdateIdentifiedDependencies(List<DependencyReference> dependencies)
    {
        // Global store for UI that is not tab-scoped (e.g. right panel with filters).
        IdentifiedDependencies.Clear();
        foreach (var dep in dependencies)
            IdentifiedDependencies.Add(dep);
        // Default selection enables details panel without extra click.
        SelectedIdentifiedDependency = IdentifiedDependencies.FirstOrDefault();
    }

    private void PushDependenciesToCodeTab(BitsTemplate template, List<DependencyReference> dependencies)
    {
        // Prefer exact tab for selected template; fallback keeps data visible if focus changed.
        var targetTab = OpenCodeTabs.FirstOrDefault(t => t.Node.Node == template) ?? SelectedCodeTab;
        if (targetTab is null) return;
        // Tab-level dependency collection is intended for line dots / per-tab popups.
        targetTab.SetDependencies(dependencies);
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



    private Dictionary<string, BitsTemplate> BuildTemplateIndex()
    {
        var dict = new Dictionary<string, BitsTemplate>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in RootNodes)
            CollectTemplates(root, dict);

        return dict;
    }

    private static void CollectTemplates(BitsNodeViewModel node, Dictionary<string, BitsTemplate> index)
    {
        if (node.Node is BitsTemplate tpl)
            index[tpl.TemplateName] = tpl;

        foreach (var child in node.Children)
            CollectTemplates(child, index);
    }
}
