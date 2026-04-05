using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.ViewModels;

/// <summary>
/// ViewModel wrapping BitsNode pre zobrazenie v TreeView.
/// Rekurzívne obaľuje Children.
/// </summary>
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

        // Priečinky sú predvolene rozbalené
        _isExpanded = node is BitsFolder;
    }
}
