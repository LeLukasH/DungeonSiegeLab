using System.Collections.ObjectModel;
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

    [ObservableProperty] private ObservableCollection<BitsNodeViewModel> _rootNodes = new();
    [ObservableProperty] private BitsNodeViewModel? _selectedNode;
    [ObservableProperty] private string _sourceCode = "Vyberte template zo stromu...";
    [ObservableProperty] private string _statusMessage = "Načítajte /Bits priečinok.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _bitsPath = "";

    private string _bitsRootPath = "";

    // Event: poslať textúry do Texture Lab
    public event Action<List<TextureReference>>? TexturesIdentified;

    // ─── Načítanie priečinka ───────────────────────────────────────────────

    [RelayCommand]
    private async Task BrowseForBitsFolderAsync(IStorageProvider? storageProvider)
    {
        if (storageProvider is null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Vyberte /Bits priečinok",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        BitsPath = folders[0].Path.LocalPath;
        await LoadBitsFolderAsync(BitsPath);
    }

    [RelayCommand]
    private async Task LoadBitsFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        IsLoading = true;
        StatusMessage = "Načítavam...";
        RootNodes.Clear();
        SourceCode = "";

        try
        {
            _bitsRootPath = path;
            var root = await _bitsLoader.LoadAsync(path);
            RootNodes.Add(new BitsNodeViewModel(root));
            StatusMessage = $"Načítané: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chyba: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Výber nodu v strome ──────────────────────────────────────────────

    partial void OnSelectedNodeChanged(BitsNodeViewModel? value)
    {
        if (value?.Node is BitsTemplate template)
        {
            SourceCode = template.SourceCode;
            StatusMessage = $"Template: {template.TemplateName}";
        }
        else if (value?.Node is BitsFile file)
        {
            SourceCode = $"// Súbor: {file.Name}\n// Vyberte template zo zoznamu.";
            StatusMessage = $"Súbor: {file.Name}";
        }
        else
        {
            SourceCode = "";
        }
    }

    // ─── Identify Textures ────────────────────────────────────────────────

    [RelayCommand]
    private void IdentifyTextures()
    {
        if (SelectedNode?.Node is not BitsTemplate template)
        {
            StatusMessage = "Najprv vyberte template.";
            return;
        }

        var textures = _textureFinder.FindInTemplate(template);

        if (!string.IsNullOrEmpty(_bitsRootPath))
            _textureFinder.ResolveTextureFiles(textures, _bitsRootPath);

        StatusMessage = $"Nájdených {textures.Count} textúr v '{template.TemplateName}'.";
        TexturesIdentified?.Invoke(textures);
    }

    // ─── Identify Template Dependencies ──────────────────────────────────

    [RelayCommand]
    private void IdentifyDependencies()
    {
        if (SelectedNode?.Node is not BitsTemplate template)
        {
            StatusMessage = "Najprv vyberte template.";
            return;
        }

        // Hľadáme "specializes = meno;" — dedičnosť templates v DS
        var specializesRegex = new System.Text.RegularExpressions.Regex(
            @"\bspecializes\s*=\s*(\w+)\s*;", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var matches = specializesRegex.Matches(template.SourceCode);
        if (matches.Count == 0)
        {
            StatusMessage = $"'{template.TemplateName}' nemá žiadne závislosti (specializes).";
            return;
        }

        var deps = string.Join(", ", matches.Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value));
        StatusMessage = $"Závislosti '{template.TemplateName}': {deps}";
    }
}
