using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;

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
    public bool IsTemplate  => Node is BitsTemplate;
    public bool IsFile      => Node is BitsFile;
    public bool IsEmptyFile => Node is BitsFile && !Children.Any();
    public bool IsFolder    => Node is BitsFolder;
    public bool IsRawFile   => Node is BitsRawFile;

    // Only these extensions are treated as readable text — everything else is binary.
    private static readonly HashSet<string> ReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".gas", ".skrit", ".cfg", ".txt", ".ini" };

    private static readonly IBrush TemplateColor      = new SolidColorBrush(Color.Parse("#cba6f7"));
    private static readonly IBrush BinaryFileColor    = new SolidColorBrush(Color.Parse("#ff0000")) { Opacity = 0.5 };
    private static readonly IBrush MutedRawFileColor  = new SolidColorBrush(Color.Parse("#7f849c"));
    private static readonly IBrush DefaultColor       = new SolidColorBrush(Color.Parse("#cdd6f4"));

    public bool IsBinaryRawFile => Node is BitsRawFile && !ReadableExtensions.Contains(Path.GetExtension(Node.Name));

    public IBrush NodeColor => Node switch
    {
        BitsTemplate                                                               => TemplateColor,
        BitsRawFile when IsBinaryRawFile                                          => BinaryFileColor,
        BitsRawFile when Path.GetExtension(Node.Name).Equals(".skrit", StringComparison.OrdinalIgnoreCase) => DefaultColor,
        BitsRawFile                                                                => MutedRawFileColor,
        _                                                                          => DefaultColor
    };

    public string Icon => Node switch
    {
        BitsFolder   => "📁",
        BitsFile     => "📄",
        BitsTemplate => "📜",
        BitsRawFile rf => RawFileIcon(rf.Name),
        _            => "•"
    };

    private static string RawFileIcon(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".tga" or ".dds" or ".bmp" or ".raw" => "🖼",
            ".wav" or ".mp3" or ".ogg" or ".aiff"                               => "🔊",
            ".skrit"                                                             => "📝",
            ".sno"                                                               => "📦",
            _ => "📎"
        };

    public ObservableCollection<BitsNodeViewModel> Children { get; } = new();

    /// <summary>Fired whenever any node's expansion state changes, so the browser can persist it.</summary>
    public static event Action? AnyExpansionChanged;

    private void CollapseWithoutNotify()
    {
#pragma warning disable MVVMTK0034
        _isExpanded = false;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(IsExpanded));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value && AppSettings.Instance.CollapseSubfoldersRecursively)
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
            if (child.IsExpanded)
                child.CollapseWithoutNotify();
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
            _            => (IEnumerable<BitsNode>)[]
        };

        if (childNodes.Any())
        {
            foreach (var child in childNodes)
                Children.Add(new BitsNodeViewModel(child));
        }

        _isExpanded = false;
    }
}
