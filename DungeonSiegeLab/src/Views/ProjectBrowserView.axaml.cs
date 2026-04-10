using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using DungeonSiegeLab.ViewModels;

namespace DungeonSiegeLab.Views;

public partial class ProjectBrowserView : UserControl
{
    public ProjectBrowserView() => InitializeComponent();

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectBrowserViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await vm.BrowseForBitsFolderCommand.ExecuteAsync(topLevel?.StorageProvider);
    }

    // Tap on the full row Border → toggle expand/collapse for folders and files
    private void OnTreeItemTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var node = (sender as Avalonia.Controls.Border)?.DataContext as BitsNodeViewModel;
        if (node is not null && !node.IsTemplate)
            node.IsExpanded = !node.IsExpanded;
    }

    // Double-tap on tree item → promote to permanent tab
    private void OnTreeDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is not ProjectBrowserViewModel vm) return;
        var node = (e.Source as Avalonia.StyledElement)?.DataContext as BitsNodeViewModel;
        if (node is not null)
            vm.PromoteToPermanent(node);
    }

    // Double-tap on tab header → promote preview tab to permanent
    private void OnTabHeaderDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var tab = (sender as Avalonia.StyledElement)?.DataContext as CodeTabViewModel;
        if (tab is not null)
            tab.IsPreview = false;
    }

    // Double-tap on search result → promote to permanent tab
    private void OnSearchResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is not ProjectBrowserViewModel vm) return;
        var result = (e.Source as Avalonia.StyledElement)?.DataContext as SearchResultViewModel;
        if (result is not null)
            vm.PromoteToPermanent(result.Node);
    }

    // Close button inside tab header — DataContext of sender is CodeTabViewModel
    private void OnCloseCodeTabClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectBrowserViewModel vm) return;
        var tab = (sender as Button)?.DataContext as CodeTabViewModel;
        if (tab is not null)
            vm.CloseCodeTabCommand.Execute(tab);
    }
}
