using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using DungeonSiegeLab.ViewModels;

namespace DungeonSiegeLab.Views;

public partial class ProjectBrowserView : UserControl
{
    public ProjectBrowserView()
    {
        InitializeComponent();
        // handledEventsToo: TreeViewItem marks PointerPressed as handled for selection,
        // which would prevent it from bubbling. We intercept it anyway.
        BitsTreeView.AddHandler(PointerPressedEvent, OnTreePointerPressed, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        UntankTreeView.AddHandler(PointerPressedEvent, OnTreePointerPressed, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnOpenRecentClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not ProjectBrowserViewModel vm) return;

        var menu = new ContextMenu();

        foreach (var path in vm.RecentPaths.Where(p => !p.Equals(vm.BitsPath, StringComparison.OrdinalIgnoreCase)))
        {
            var item = new MenuItem { Header = path, Icon = new TextBlock { Text = "📁", FontSize = 13 } };
            var captured = path;
            item.Click += (_, _) => vm.OpenRecentCommand.Execute(captured);
            menu.Items.Add(item);
        }

        menu.Open(button);
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectBrowserViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        await vm.BrowseForBitsFolderCommand.ExecuteAsync(topLevel?.StorageProvider);
    }

    // Any click on a tree row → toggle expand/collapse, except the built-in expander arrow
    private void OnTreePointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;

        // Skip if the click landed on (or inside) the expander ToggleButton
        var source = e.Source as Avalonia.Controls.Control;
        while (source != null)
        {
            if (source is Avalonia.Controls.Primitives.ToggleButton) return;
            source = source.Parent as Avalonia.Controls.Control;
        }

        // Walk up to find the BitsNodeViewModel
        var target = e.Source as Avalonia.Controls.Control;
        while (target != null)
        {
            if (target.DataContext is BitsNodeViewModel node)
            {
                if (!node.IsTemplate)
                    node.IsExpanded = !node.IsExpanded;
                return;
            }
            target = target.Parent as Avalonia.Controls.Control;
        }
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
