using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

public partial class TextureLabViewModel : ViewModelBase
{
    private readonly RawTextureConverter _converter = new();

    [ObservableProperty] private ObservableCollection<TextureTabViewModel> _openTabs = new();
    [ObservableProperty] private TextureTabViewModel? _selectedTab;
    [ObservableProperty] private string _statusMessage = "No texture is open.";
    [ObservableProperty] private string _rawToPsdToolPath = "";

    public event Action? BackRequested;

    // ─── Load textures from Project Browser ──────────────────────────────

    public async Task LoadTexturesAsync(List<TextureReference> textures)
    {
        _converter.SetToolPath(RawToPsdToolPath);

        foreach (var tex in textures.Where(t => t.ResolvedPath != null))
        {
            if (OpenTabs.Any(t => t.TextureName == tex.TextureName))
                continue;

            var tab = new TextureTabViewModel { TextureName = tex.TextureName };
            OpenTabs.Add(tab);
            SelectedTab = tab;

            await tab.LoadAsync(_converter, tex);
        }

        StatusMessage = OpenTabs.Count > 0
            ? $"{OpenTabs.Count} texture(s) open."
            : "No textures could be loaded (files not found in /Bits).";
    }

    // ─── Open texture manually ────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenTextureManuallyAsync(IStorageProvider? storageProvider)
    {
        if (storageProvider is null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Texture",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Dungeon Siege Textures") { Patterns = new[] { "*.raw", "*.psd" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count == 0) return;

        var filePath = files[0].Path.LocalPath;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var tab = new TextureTabViewModel { TextureName = name };
        OpenTabs.Add(tab);
        SelectedTab = tab;

        _converter.SetToolPath(RawToPsdToolPath);
        await tab.LoadFromPathAsync(_converter, filePath);
    }

    // ─── Close tab ────────────────────────────────────────────────────────

    [RelayCommand]
    private void CloseTab(TextureTabViewModel? tab)
    {
        if (tab is null) return;
        tab.Dispose();
        OpenTabs.Remove(tab);
        SelectedTab = OpenTabs.LastOrDefault();
        StatusMessage = OpenTabs.Count == 0 ? "No texture is open." : "";
    }

    // ─── Save to disk ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveToDiskAsync(IStorageProvider? storageProvider)
    {
        if (SelectedTab?.Texture is null || storageProvider is null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Texture as PNG",
            SuggestedFileName = SelectedTab.TextureName + ".png",
            FileTypeChoices = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } }
        });

        if (file is null) return;

        try
        {
            await _converter.ExportToDiskAsync(SelectedTab.Texture, file.Path.LocalPath);
            StatusMessage = $"Saved: {file.Path.LocalPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving: {ex.Message}";
        }
    }

    // ─── Import replacement ───────────────────────────────────────────────

    [RelayCommand]
    private async Task ImportReplacementAsync(IStorageProvider? storageProvider)
    {
        if (SelectedTab is null || storageProvider is null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Replacement Texture (PNG)",
            FileTypeFilter = new[] { new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } } }
        });

        if (files.Count == 0) return;

        try
        {
            var newTexture = await _converter.ImportReplacementAsync(
                files[0].Path.LocalPath, SelectedTab.TextureName);
            SelectedTab.UpdateTexture(newTexture);
            StatusMessage = $"Replacement imported for '{SelectedTab.TextureName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while importing: {ex.Message}";
        }
    }

    // ─── RawToPsd tool path ───────────────────────────────────────────────

    [RelayCommand]
    private async Task BrowseForToolAsync(IStorageProvider? storageProvider)
    {
        if (storageProvider is null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select RawToPsd.exe",
            FileTypeFilter = new[] { new FilePickerFileType("Executable") { Patterns = new[] { "*.exe", "*" } } }
        });

        if (files.Count > 0)
            RawToPsdToolPath = files[0].Path.LocalPath;
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}

// ─── TextureTabViewModel ──────────────────────────────────────────────────────

public partial class TextureTabViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty] private string _textureName = "";
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _dimensions = "–";
    [ObservableProperty] private string _status = "Loading...";
    [ObservableProperty] private string _standardUsage = "–";
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _errorMessage = "";

    public LoadedTexture? Texture { get; private set; }

    public async Task LoadAsync(RawTextureConverter converter, TextureReference texRef)
    {
        try
        {
            Texture = await converter.LoadTextureAsync(texRef);
            ApplyTexture();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = "Error";
            IsLoading = false;
        }
    }

    public async Task LoadFromPathAsync(RawTextureConverter converter, string filePath)
    {
        try
        {
            Texture = await converter.LoadFromPathAsync(TextureName, filePath);
            ApplyTexture();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Status = "Error";
            IsLoading = false;
        }
    }

    public void UpdateTexture(LoadedTexture newTexture)
    {
        Texture = newTexture;
        ApplyTexture();
    }

    private void ApplyTexture()
    {
        if (Texture is null) return;

        Dimensions = $"{Texture.Width}×{Texture.Height}";
        Status = Texture.StatusText;
        StandardUsage = Texture.StandardUsage;
        IsLoading = false;

        if (Texture.PngCachePath != null && File.Exists(Texture.PngCachePath))
        {
            PreviewImage?.Dispose();
            PreviewImage = new Bitmap(Texture.PngCachePath);
        }
    }

    public void Dispose()
    {
        PreviewImage?.Dispose();
        if (Texture?.PngCachePath != null && File.Exists(Texture.PngCachePath))
            File.Delete(Texture.PngCachePath);
    }
}
