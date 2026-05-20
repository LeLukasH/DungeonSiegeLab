using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using DungeonSiegeLab.ViewModels;

namespace DungeonSiegeLab.Views;

public partial class TextureLabView : UserControl
{
    public TextureLabView() => InitializeComponent();

    private async void OnOpenTextureClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TextureLabViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await vm.OpenTextureManuallyCommand.ExecuteAsync(topLevel?.StorageProvider);
    }

    private async void OnSaveAsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TextureLabViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await vm.SaveAsCommand.ExecuteAsync(topLevel?.StorageProvider);
    }

    private async void OnImportReplacementClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TextureLabViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await vm.ImportReplacementCommand.ExecuteAsync(topLevel?.StorageProvider);
    }

    private void OnCloseTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TextureLabViewModel vm) return;
        var button = sender as Button;
        // Nájdeme DataContext záložky (TextureTabViewModel) cez vizuálny strom
        var tab = (button?.DataContext as TextureTabViewModel)
               ?? (button?.TemplatedParent as ContentPresenter)?.DataContext as TextureTabViewModel;
        if (tab is not null)
            vm.CloseTabCommand.Execute(tab);
    }

    private async void OnSaveToProjectClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not TextureLabViewModel vm) return;
        if (vm.SelectedTab?.Texture is null) return;

        // Hľadáme bitsRootPath – prístup cez MainViewModel
        var mainWindow = TopLevel.GetTopLevel(this) as Window;
        var mainVm = mainWindow?.DataContext as ViewModels.MainViewModel;
        var bitsRoot = mainVm?.ProjectBrowser.BitsPath ?? "";

        var dialog = new SaveToProjectDialog();
        var dialogVm = new SaveToProjectViewModel(
            new Services.RawTextureConverter(),
            vm.SelectedTab.Texture,
            bitsRoot);
        dialog.DataContext = dialogVm;
        dialogVm.CloseRequested += () => dialog.Close();

        await dialog.ShowDialog(mainWindow ?? new Window());
    }
}
