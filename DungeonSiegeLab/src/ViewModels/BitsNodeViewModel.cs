using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.ViewModels;

public partial class BitsNodeViewModel : ViewModelBase
{
    public BitsNode Node { get; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public string Name => Node.Name;
    public string FullPath => Node.FullPath;
    public bool IsTemplate => Node is BitsTemplate;
    public bool IsFile => Node is BitsFile;
    public bool IsFolder => Node is BitsFolder;

    public string Icon => Node switch
    {
        BitsFolder => "📁",
        BitsFile   => "📄",
        BitsTemplate => "📜",
        _ => "•"
    };

    public ObservableCollection<BitsNodeViewModel> Children { get; } = new();

    /// <summary>Fired whenever any node's expansion state changes, so the browser can persist it.</summary>
    public static event Action? AnyExpansionChanged;

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value)
            ForceCollapseDescendants(Children);
        AnyExpansionChanged?.Invoke();
    }

    /// <summary>
    /// Collapses all descendants by directly setting backing fields (no recursive events),
    /// then notifies the UI via PropertyChanged so the TwoWay binding updates TreeViewItems.
    /// Only one AnyExpansionChanged fires — from the node that was explicitly collapsed.
    /// </summary>
    private static void ForceCollapseDescendants(ObservableCollection<BitsNodeViewModel> nodes)
    {
        foreach (var child in nodes)
        {
            if (child._isExpanded)
            {
                child._isExpanded = false;
                child.OnPropertyChanged(nameof(IsExpanded));
            }
            if (child.Children.Count > 0)
                ForceCollapseDescendants(child.Children);
        }
    }

    public BitsNodeViewModel(BitsNode node)
    {
        Node = node;

        var childNodes = node switch
        {
            BitsFolder f => f.Children,
            BitsFile fi  => fi.Children,
            _ => null
        };

        if (childNodes != null)
        {
            foreach (var child in childNodes)
                Children.Add(new BitsNodeViewModel(child));
        }

        _isExpanded = false;
    }
}
