using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

public partial class ProjectBrowserViewModel : ViewModelBase
{
    private readonly IBitsLoader _loader = new BitsLoader();
    private readonly TextureFinder _textureFinder = new();
    private readonly DependencyFinder _dependencyFinder = new();

    private readonly TreeStateService _treeState = TreeStateService.Instance;
    private CancellationTokenSource? _searchCts;

    /// <summary>Bundled base-game data folder, placed next to the executable.</summary>
    public static string UntankPath => BitsLoader.UntankPath;

    /// <summary>Fixed key used in state file for Untank — machine-independent.</summary>
    private const string UntankStateKey = "Untank";

    [ObservableProperty] private ObservableCollection<BitsComponentViewModel> _rootNodes = new();
    public ObservableCollection<BitsComponentViewModel> BitsRootNodes { get; } = new();
    public ObservableCollection<BitsComponentViewModel> UntankRootNodes { get; } = new();
    public bool HasUntankSection => UntankRootNodes.Count > 0;
    [ObservableProperty] private BitsComponentViewModel? _selectedNode;
    [ObservableProperty] private string _statusMessage = "Open a /Bits folder.";
    [ObservableProperty] private string _statusDetail  = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _bitsPath = "";

    [ObservableProperty] private ObservableCollection<CodeTabViewModel> _openCodeTabs = new();
    [ObservableProperty] private CodeTabViewModel? _selectedCodeTab;
    [ObservableProperty] private ObservableCollection<DependencyReference> _identifiedDependencies = new();
    [ObservableProperty] private DependencyReference? _selectedIdentifiedDependency;
    [ObservableProperty] private bool _isDependencyPanelOpen;

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
    private BitsComponentViewModel? _bitsRootVm;
    private BitsComponentViewModel? _untankRootVm;
    private CancellationTokenSource? _saveCts;

    public ObservableCollection<string> RecentPaths { get; } = new();
    public bool HasRecentPaths => RecentPaths.Any(p => !p.Equals(BitsPath, StringComparison.OrdinalIgnoreCase));

    public event Action<List<TextureReference>>? TexturesIdentified;
    public event Action<List<DependencyReference>>? DependenciesIdentified;

    public ProjectBrowserViewModel()
    {
        _treeState.Load();
        BitsPath = _treeState.LastBitsPath ?? "";

        foreach (var p in _treeState.RecentPaths)
            RecentPaths.Add(p);

        BitsComponentViewModel.AnyExpansionChanged += SaveExpansionStateDebounced;
        OpenCodeTabs.CollectionChanged += OnOpenCodeTabsChanged;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadUntankAsync(restoreExpansion: true);
        if (!string.IsNullOrEmpty(BitsPath))
            await LoadCoreAsync(BitsPath, restoreExpansion: true);
    }

    private void OnOpenCodeTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasOpenCodeTabs));
        OnPropertyChanged(nameof(HasNoOpenCodeTabs));
    }

    // ─── Load user's Bits folder ──────────────────────────────────────────

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

    [RelayCommand]
    private Task OpenRecentAsync(string path)
    {
        BitsPath = path;
        return LoadCoreAsync(path, restoreExpansion: true);
    }

    private async Task LoadCoreAsync(string path, bool restoreExpansion)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        IsLoading = true;
        StatusMessage = "Loading...";
        OpenCodeTabs.Clear();
        SelectedCodeTab = null;

        // Save the departing folder's expansion state immediately before switching
        if (!string.IsNullOrEmpty(_bitsRootPath) && !_bitsRootPath.Equals(path, StringComparison.OrdinalIgnoreCase) && _bitsRootVm != null)
        {
            _saveCts?.Cancel();
            _treeState.SaveExpansionState(BitsStateKey, ToRelativeBitsPaths(CollectExpandedPaths([_bitsRootVm])));
        }

        // Rebuild root list: clear old Bits root, keep Untank
        _bitsRootVm = null;
        BitsRootNodes.Clear();
        RootNodes.Clear();
        if (_untankRootVm != null)
            RootNodes.Add(_untankRootVm);

        try
        {
            _bitsRootPath = path;
            var progress = new Progress<(int percent, string folder)>(p =>
            {
                StatusMessage = $"Loading… {p.percent}%";
                StatusDetail  = p.folder;
            });
            BitsFolder root = await _loader.LoadAsync(path, progress);
            _bitsRootVm = BitsComponentViewModel.Create(root);

            // Bits goes first — insert before Untank
            RootNodes.Insert(0, _bitsRootVm);
            BitsRootNodes.Add(_bitsRootVm);
            OnPropertyChanged(nameof(HasUntankSection));

            if (restoreExpansion)
            {
                var savedRelative = _treeState.GetExpandedPaths(BitsStateKey);
                if (savedRelative.Count > 0)
                {
                    var saved = ToAbsoluteBitsPaths(savedRelative);
                    BitsComponentViewModel.AnyExpansionChanged -= SaveExpansionStateDebounced;
                    RestoreExpansionState(new[] { _bitsRootVm }, saved);
                    BitsComponentViewModel.AnyExpansionChanged += SaveExpansionStateDebounced;
                }
            }

            _treeState.SaveExpansionState(BitsStateKey, ToRelativeBitsPaths(CollectExpandedPaths([_bitsRootVm])));
            _treeState.AddRecentPath(path);
            RefreshRecentPaths();
            // Post at Background priority so it runs after any remaining Progress<T> callbacks
            Dispatcher.UIThread.Post(() => { StatusMessage = "Loading Completed"; StatusDetail = path; },
                DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            StatusDetail  = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnBitsPathChanged(string value) => OnPropertyChanged(nameof(HasRecentPaths));

    private void RefreshRecentPaths()
    {
        RecentPaths.Clear();
        foreach (var p in _treeState.RecentPaths)
            RecentPaths.Add(p);
        OnPropertyChanged(nameof(HasRecentPaths));
    }

    // ─── Load bundled Untank folder ───────────────────────────────────────

    private async Task LoadUntankAsync(bool restoreExpansion = false)
    {
        if (!Directory.Exists(UntankPath)) return;

        try
        {
            StatusMessage = "Loading Untank…";
            StatusDetail  = UntankPath;

            var progress = new Progress<(int percent, string folder)>(p =>
            {
                StatusMessage = $"Loading Untank… {p.percent}%";
                StatusDetail  = p.folder;
            });
            BitsFolder root = await _loader.LoadAsync(UntankPath, progress);
            _untankRootVm = BitsComponentViewModel.Create(root);

            if (restoreExpansion)
            {
                var savedRelative = _treeState.GetExpandedPaths(UntankStateKey);
                if (savedRelative.Count > 0)
                {
                    var saved = ToAbsoluteUntankPaths(savedRelative);
                    BitsComponentViewModel.AnyExpansionChanged -= SaveExpansionStateDebounced;
                    RestoreExpansionState(new[] { _untankRootVm }, saved);
                    BitsComponentViewModel.AnyExpansionChanged += SaveExpansionStateDebounced;
                }
            }

            // Untank always goes last
            RootNodes.Add(_untankRootVm);
            UntankRootNodes.Clear();
            UntankRootNodes.Add(_untankRootVm);
            OnPropertyChanged(nameof(HasUntankSection));
            Dispatcher.UIThread.Post(() => { StatusMessage = "Loading Completed"; StatusDetail = UntankPath; },
                DispatcherPriority.Background);
        }
        catch { /* Untank load failure is non-critical */ }
    }

    // ─── Expansion state ──────────────────────────────────────────────────

    private static void RestoreExpansionState(IEnumerable<BitsComponentViewModel> roots, HashSet<string> expandedPaths)
    {
        foreach (var root in roots)
            root.RestoreExpansion(expandedPaths);
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
                _treeState.SaveExpansionState(BitsStateKey, ToRelativeBitsPaths(CollectExpandedPaths([_bitsRootVm])));

            if (_untankRootVm != null)
                _treeState.SaveExpansionOnly(UntankStateKey, ToRelativeUntankPaths(CollectExpandedPaths([_untankRootVm])));
        }
        catch (OperationCanceledException) { }
    }

    // ── Path helpers (relative storage) ──────────────────────────────────
    // Values stored as "." (root) or "/sub/folder" (relative with leading slash).
    // Bits key = full absolute path. Untank key = "Untank".

    /// <summary>State key for the Bits folder — its full absolute path.</summary>
    private string BitsStateKey => _bitsRootPath;

    private static IEnumerable<string> ToRelativePaths(IEnumerable<string> absolute, string basePath)
    {
        var base_ = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return absolute.Select(p =>
        {
            var norm = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (norm.Equals(base_, StringComparison.OrdinalIgnoreCase))
                return "";
            if (norm.StartsWith(base_, StringComparison.OrdinalIgnoreCase))
                return norm[base_.Length..].Replace('\\', '/');
            return p;
        });
    }

    private static HashSet<string> ToAbsolutePaths(HashSet<string> relative, string basePath)
    {
        var base_ = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relative.Select(p => p switch
        {
            ""                           => base_,
            _ when p.StartsWith('/')     => base_ + p.Replace('/', Path.DirectorySeparatorChar),
            _                            => p
        }).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> ToRelativeBitsPaths(IEnumerable<string> a)   => ToRelativePaths(a, _bitsRootPath);
    private HashSet<string>     ToAbsoluteBitsPaths(HashSet<string> r)        => ToAbsolutePaths(r, _bitsRootPath);
    private IEnumerable<string> ToRelativeUntankPaths(IEnumerable<string> a)  => ToRelativePaths(a, UntankPath);
    private HashSet<string>     ToAbsoluteUntankPaths(HashSet<string> r)      => ToAbsolutePaths(r, UntankPath);

    private static List<string> CollectExpandedPaths(IEnumerable<BitsComponentViewModel> roots)
    {
        return [..roots.SelectMany(root => root.ExpandedPaths())];
    }

    // ─── Tree selection (single click → preview tab) ──────────────────────

    public bool CanIdentify => SelectedNode?.CanIdentify == true;

    partial void OnSelectedNodeChanged(BitsComponentViewModel? value)
    {
        OnPropertyChanged(nameof(CanIdentify));

        if (value is null) return;
        StatusMessage = value.StatusText;
        if (value.CanOpenPreview)
            OpenPreviewTab(value);
    }

    public void OpenPreviewTab(BitsComponentViewModel node)
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

    public void PromoteToPermanent(BitsComponentViewModel node)
    {
        if (!node.CanOpenPreview) return;

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

    /// PATTERN: Command - Identify is triggered from UI via RelayCommand binding.
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
        // PATTERN: Observer - event notifies external listeners that new dependencies are available.
        UpdateIdentifiedDependencies(dependencies);
        PushDependenciesToCodeTab(template, dependencies);
        DependenciesIdentified?.Invoke(dependencies);
        IsDependencyPanelOpen = true;

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
        // PATTERN: Adapter - convert parser output into frontend-oriented observable state.
        // Global store for UI that is not tab-scoped (e.g. right panel with filters).
        IdentifiedDependencies.Clear();
        foreach (var dep in dependencies)
            IdentifiedDependencies.Add(dep);
        // Default selection enables details panel without extra click.
        SelectedIdentifiedDependency = IdentifiedDependencies.FirstOrDefault();
    }

    private void PushDependenciesToCodeTab(BitsTemplate template, List<DependencyReference> dependencies)
    {
        // PATTERN: Adapter - project shared identify output into per-tab view model state.
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
                () => DoSearch(roots, UntankPath, query, inNames, inContent), token);

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
        List<BitsComponentViewModel> roots, string untankPath, string query, bool inNames, bool inContent)
    {
        var bitsResults   = new List<SearchResultViewModel>();
        var untankResults = new List<SearchResultViewModel>();
        var seen          = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            bool isUntank = root.FullPath.Equals(untankPath, StringComparison.OrdinalIgnoreCase);
            var bucket = isUntank ? untankResults : bitsResults;
            root.ForEach(vm => TryAddSearchResult(vm, root.FullPath, query, inNames, inContent, bucket, seen, isUntank));
        }

        // Re-stamp IsFirstUntankResult on the first Untank hit (needs a divider above it)
        bool needsDivider = bitsResults.Count > 0 && untankResults.Count > 0;
        for (int i = 0; i < untankResults.Count; i++)
        {
            var r = untankResults[i];
            untankResults[i] = new SearchResultViewModel(r.Node, r.RelativePath, r.MatchSnippets, r.Query)
            {
                IsUntankSource = true,
                IsFirstUntankResult = needsDivider && i == 0
            };
        }

        bitsResults.AddRange(untankResults);
        return bitsResults;
    }

    private static void TryAddSearchResult(
        BitsComponentViewModel node, string rootPath,
        string query, bool inNames, bool inContent,
        List<SearchResultViewModel> results, HashSet<string> seen, bool isUntank)
    {
        if (node.Node is not BitsTemplate template) return;

        bool nameMatch = inNames && node.Name.Contains(query, StringComparison.OrdinalIgnoreCase);

        List<string> snippets = [];
        bool contentMatch = false;
        if (inContent)
        {
            snippets = FindMatchingLines(template.SourceCode, query);
            contentMatch = snippets.Count > 0;
        }

        if ((nameMatch || contentMatch) && seen.Add(node.FullPath))
        {
            var rel = node.FullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
                ? node.FullPath[rootPath.Length..].TrimStart('/', '\\')
                : node.FullPath;
            results.Add(new SearchResultViewModel(node, rel, snippets, query) { IsUntankSource = isUntank });
        }
    }

    private static List<string> FindMatchingLines(string sourceCode, string query)
    {
        var results = new List<string>();
        foreach (var rawLine in sourceCode.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                results.Add(line.Length > 80 ? line[..80] + "…" : line);
        }
        return results;
    }



    /// PATTERN: Composite - recursive traversal over tree nodes to collect templates.
    private Dictionary<string, BitsTemplate> BuildTemplateIndex()
    {
        var dict = new Dictionary<string, BitsTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in RootNodes)
            foreach (var tpl in root.Node.FindAll<BitsTemplate>())
                dict[tpl.TemplateName] = tpl;
        return dict;
    }

    /// PATTERN: Composite + Iterator - depth-first walk over node children.
    private static void CollectTemplates(BitsComponentViewModel node, Dictionary<string, BitsTemplate> index)
    {
        if (node.Node is BitsTemplate tpl)
            index[tpl.TemplateName] = tpl;

        foreach (var child in node.Children)
            CollectTemplates(child, index);
    }
}
