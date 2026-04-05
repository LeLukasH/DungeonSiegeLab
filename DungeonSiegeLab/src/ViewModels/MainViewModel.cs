using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ProjectBrowserViewModel ProjectBrowser { get; } = new();
    public TextureLabViewModel TextureLab { get; } = new();

    [ObservableProperty] private int _selectedTabIndex = 0; // 0 = Browser, 1 = Lab

    public MainViewModel()
    {
        // Keď Browser identifikuje textúry, prepni na Lab a načítaj ich
        ProjectBrowser.TexturesIdentified += OnTexturesIdentified;

        // Keď Lab požiada o návrat, prepni na Browser
        TextureLab.BackRequested += () => SelectedTabIndex = 0;
    }

    private async void OnTexturesIdentified(List<TextureReference> textures)
    {
        SelectedTabIndex = 1;
        await TextureLab.LoadTexturesAsync(textures);
    }

    // Priamy príkaz na prepnutie tabu (pre tlačidlá v UI)
    [RelayCommand]
    private void SwitchToLab() => SelectedTabIndex = 1;

    [RelayCommand]
    private void SwitchToBrowser() => SelectedTabIndex = 0;
}
