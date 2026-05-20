using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;
<<<<<<< HEAD
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
=======
using DungeonSiegeLab.Services;
>>>>>>> bd25586769c769fdf11afd06ad97eb070932104f

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
<<<<<<< HEAD
        Console.WriteLine("MainViewModel initialized and waiting for Bits folder load.");
=======
        TreeStateService.Instance.Load();
        AppSettings.Instance.CollapseSubfoldersRecursively = TreeStateService.Instance.CollapseSubfoldersRecursively;

>>>>>>> bd25586769c769fdf11afd06ad97eb070932104f
        ProjectBrowser.TexturesIdentified += OnTexturesIdentified;
        ProjectBrowser.BitsFolderLoaded += OnBitsFolderLoaded;
        TextureLab.BackRequested += () => SelectedTabIndex = 0;
    }

    private async void OnTexturesIdentified(List<TextureReference> textures)
    {
        SelectedTabIndex = 1;
        await TextureLab.LoadTexturesAsync(textures);
    }

    private void OnBitsFolderLoaded(string path)
    {
        Console.WriteLine($"Bits folder loaded: {path}");
    }

    [RelayCommand] private void SwitchToLab()      => SelectedTabIndex = 1;
    [RelayCommand] private void SwitchToBrowser()  => SelectedTabIndex = 0;
    [RelayCommand] private void SwitchToSettings() => SelectedTabIndex = 2;
}
