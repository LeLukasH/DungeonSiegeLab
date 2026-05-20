using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

public partial class SaveToProjectViewModel : ViewModelBase
{
    private readonly RawTextureConverter _converter;
    private readonly LoadedTexture _texture;
    private readonly string _bitsRootPath;

    [ObservableProperty] private string _relativePath = "world/art/bitmaps/";
    [ObservableProperty] private string _textureName = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isStatusOk;
    [ObservableProperty] private bool _isSaving;

    public event Action? CloseRequested;

    public SaveToProjectViewModel(RawTextureConverter converter, LoadedTexture texture, string bitsRootPath)
    {
        _converter = converter;
        _texture = texture;
        _bitsRootPath = bitsRootPath;
        _textureName = texture.Name;
        ValidateName();
    }

    partial void OnTextureNameChanged(string value) => ValidateName();
    partial void OnRelativePathChanged(string value) => ValidateName();

    private void ValidateName()
    {
        if (string.IsNullOrWhiteSpace(TextureName))
        {
            StatusMessage = "Enter a texture name.";
            IsStatusOk = false;
            return;
        }

        var fullPath = Path.Combine(_bitsRootPath, RelativePath, TextureName + ".raw");
        if (File.Exists(fullPath))
        {
            StatusMessage = $"Warning: '{TextureName}.raw' already exists and will be overwritten.";
            IsStatusOk = false;
        }
        else
        {
            StatusMessage = $"Will be saved as: {RelativePath}{TextureName}.raw";
            IsStatusOk = true;
        }
    }

    [RelayCommand]
    private async Task ExportToProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(TextureName)) return;

        IsSaving = true;
        try
        {
            await _converter.SaveToProjectAsync(_texture, _bitsRootPath, RelativePath, TextureName);
            StatusMessage = $"Saved: {RelativePath}{TextureName}.raw";
            IsStatusOk = true;
            await Task.Delay(800);
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsStatusOk = false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
