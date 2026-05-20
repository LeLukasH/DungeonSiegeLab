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

    public bool HasOpenTabs => OpenTabs.Count > 0;

    public event Action? BackRequested;

    public async Task LoadTexturesAsync(List<TextureReference> textures)
    {
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
        OnPropertyChanged(nameof(HasOpenTabs));
    }

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
                new FilePickerFileType("Supported Textures") { Patterns = new[] { "*.png", "*.psd", "*.raw" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count == 0) return;

        var filePath = files[0].Path.LocalPath;
        var name = Path.GetFileNameWithoutExtension(filePath);
        var tab = new TextureTabViewModel { TextureName = name };
        OpenTabs.Add(tab);
        SelectedTab = tab;

        await tab.LoadFromPathAsync(_converter, filePath);
        StatusMessage = $"Opened: {Path.GetFileName(filePath)}";
        OnPropertyChanged(nameof(HasOpenTabs));
    }

    [RelayCommand]
    private void CloseTab(TextureTabViewModel? tab)
    {
        if (tab is null) return;
        tab.Dispose();
        OpenTabs.Remove(tab);
        SelectedTab = OpenTabs.LastOrDefault();
        StatusMessage = OpenTabs.Count == 0 ? "No texture is open." : "";
        OnPropertyChanged(nameof(HasOpenTabs));
    }

    [RelayCommand]
    private async Task SaveAsAsync(IStorageProvider? storageProvider)
    {
        if (SelectedTab?.Texture is null || storageProvider is null) return;

        var currentExtension = SelectedTab.Texture.OriginalFormat.ToExtension();
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Texture As",
            SuggestedFileName = SelectedTab.TextureName + currentExtension,
            DefaultExtension = currentExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("PNG") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("PSD") { Patterns = new[] { "*.psd" } },
                new FilePickerFileType("RAW") { Patterns = new[] { "*.raw" } }
            }
        });

        if (file is null) return;

        try
        {
            await _converter.SaveAsAsync(SelectedTab.Texture, file.Path.LocalPath);
            StatusMessage = $"Saved: {file.Path.LocalPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportReplacementAsync(IStorageProvider? storageProvider)
    {
        if (SelectedTab is null || storageProvider is null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Replacement Texture",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Supported Textures") { Patterns = new[] { "*.png", "*.psd", "*.raw" } }
            }
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

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}

public partial class TextureTabViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty] private string _textureName = "";
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private string _dimensions = "-";
    [ObservableProperty] private string _status = "Loading...";
    [ObservableProperty] private string _standardUsage = "-";
    [ObservableProperty] private string _formatLabel = "-";
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
        DisposeTextureResources();
        Texture = newTexture;
        ApplyTexture();
    }

    private void ApplyTexture()
    {
        if (Texture is null) return;

        Dimensions = $"{Texture.Width}x{Texture.Height}";
        Status = Texture.StatusText;
        StandardUsage = Texture.StandardUsage;
        FormatLabel = Texture.OriginalFormat.ToString().ToUpperInvariant();
        IsLoading = false;
        ErrorMessage = "";

        if (Texture.PngCachePath != null && File.Exists(Texture.PngCachePath))
        {
            PreviewImage?.Dispose();
            PreviewImage = new Bitmap(Texture.PngCachePath);
        }
    }

    public void Dispose()
    {
        DisposeTextureResources();
    }

    private void DisposeTextureResources()
    {
        PreviewImage?.Dispose();
        PreviewImage = null;

        SafeDelete(Texture?.PngCachePath);

        if (Texture?.WorkingPsdPath != null &&
            !string.Equals(Texture.WorkingPsdPath, Texture.OriginalPath, StringComparison.OrdinalIgnoreCase))
            SafeDelete(Texture.WorkingPsdPath);
    }

    private static void SafeDelete(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors for temp files.
        }
    }
}
