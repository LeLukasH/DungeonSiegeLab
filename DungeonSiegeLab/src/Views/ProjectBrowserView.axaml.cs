using Avalonia.Controls;
using DungeonSiegeLab.ViewModels;

namespace DungeonSiegeLab.Views;

public partial class ProjectBrowserView : UserControl
{
    public ProjectBrowserView() => InitializeComponent();

    // Handler pre "Prehľadávať..." tlačidlo – potrebujeme prístup k StorageProvider
    private async void OnBrowseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ProjectBrowserViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await vm.BrowseForBitsFolderCommand.ExecuteAsync(topLevel?.StorageProvider);
    }
}
