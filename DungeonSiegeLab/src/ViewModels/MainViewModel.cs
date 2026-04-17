using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ProjectBrowserViewModel ProjectBrowser { get; } = new();
    public TextureLabViewModel TextureLab { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty] private int _selectedTabIndex = 0; // 0 = Browser, 1 = Lab, 2 = Settings

    public bool IsProjectBrowserActive => SelectedTabIndex == 0;
    public bool IsTextureLabActive     => SelectedTabIndex == 1;
    public bool IsSettingsActive       => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsProjectBrowserActive));
        OnPropertyChanged(nameof(IsTextureLabActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    public MainViewModel()
    {
        ProjectBrowser.TexturesIdentified += OnTexturesIdentified;
        TextureLab.BackRequested += () => SelectedTabIndex = 0;
    }

    private async void OnTexturesIdentified(List<TextureReference> textures)
    {
        SelectedTabIndex = 1;
        await TextureLab.LoadTexturesAsync(textures);
    }

    [RelayCommand] private void SwitchToLab()      => SelectedTabIndex = 1;
    [RelayCommand] private void SwitchToBrowser()  => SelectedTabIndex = 0;
    [RelayCommand] private void SwitchToSettings() => SelectedTabIndex = 2;
}
