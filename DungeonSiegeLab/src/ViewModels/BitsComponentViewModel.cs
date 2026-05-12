using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

// ── Abstract base ─────────────────────────────────────────────────────────────

public abstract partial class BitsComponentViewModel : ViewModelBase
{
    public BitsComponent Node { get; }

    protected BitsComponentViewModel(BitsComponent node) { Node = node; }

    [ObservableProperty] private bool   _isExpanded;
    [ObservableProperty] private bool   _isSelected;
    [ObservableProperty] private IBrush _nodeColor = DefaultColor;

    public string Name     => Node.Name;
    public string FullPath => Node.FullPath;

    public virtual bool CanExpand      => false;
    public virtual bool CanOpenPreview => false;
    public virtual bool CanIdentify    => false;

    public abstract string StatusText { get; }
    public abstract string Icon       { get; }
    public abstract void   ApplyColor();

    public abstract ObservableCollection<BitsComponentViewModel> Children { get; }

    public abstract void CollapseAll();
    public abstract void RestoreExpansion(HashSet<string> expandedPaths);
    public abstract IEnumerable<string> ExpandedPaths();
    public abstract void ForEach(Action<BitsComponentViewModel> action);

    public static event Action? AnyExpansionChanged;

    protected static readonly IBrush DefaultColor = new SolidColorBrush(Color.Parse("#cdd6f4"));

    protected static readonly HashSet<string> ReadableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".gas", ".skrit", ".cfg", ".txt", ".ini" };

    protected void CollapseWithoutNotify()
    {
#pragma warning disable MVVMTK0034
        _isExpanded = false;
#pragma warning restore MVVMTK0034
        OnPropertyChanged(nameof(IsExpanded));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value && AppSettings.Instance.CollapseSubfoldersRecursively)
            CollapseAll();
        AnyExpansionChanged?.Invoke();
    }

    public static BitsComponentViewModel Create(BitsComponent node) => node switch
    {
        BitsFolder   f => new BitsFolderViewModel(f),
        BitsFile    fi => new BitsFileViewModel(fi),
        BitsTemplate t => new BitsTemplateViewModel(t),
        BitsRawFile  r => new BitsRawFileViewModel(r),
        _              => throw new ArgumentException($"Unknown node type: {node.GetType().Name}")
    };
}

// ── Composite ─────────────────────────────────────────────────────────────────

public abstract class BitsCompositeViewModel : BitsComponentViewModel
{
    private readonly ObservableCollection<BitsComponentViewModel> _children = [];
    public override ObservableCollection<BitsComponentViewModel> Children => _children;

    public override bool CanExpand => Children.Count > 0;

    protected BitsCompositeViewModel(BitsComposite node) : base(node)
    {
        foreach (var child in node.Children)
            _children.Add(Create(child));
        NodeColor = DefaultColor;
    }

    public override void ApplyColor()
    {
        NodeColor = DefaultColor;
        foreach (var child in Children)
            child.ApplyColor();
    }

    public override void CollapseAll()
    {
        if (IsExpanded) CollapseWithoutNotify();
        foreach (var child in Children)
            child.CollapseAll();
    }

    public override void RestoreExpansion(HashSet<string> expandedPaths)
    {
        IsExpanded = expandedPaths.Contains(FullPath);
        foreach (var child in Children)
            child.RestoreExpansion(expandedPaths);
    }

    public override IEnumerable<string> ExpandedPaths()
    {
        if (IsExpanded) yield return FullPath;
        foreach (var child in Children)
            foreach (var path in child.ExpandedPaths())
                yield return path;
    }

    public override void ForEach(Action<BitsComponentViewModel> action)
    {
        action(this);
        foreach (var child in Children)
            child.ForEach(action);
    }
}

public class BitsFolderViewModel : BitsCompositeViewModel
{
    public BitsFolderViewModel(BitsFolder node) : base(node) { }

    public override string StatusText => $"Folder: {Name}";
    public override string Icon       => "📁";
}

public class BitsFileViewModel : BitsCompositeViewModel
{
    public bool IsEmptyFile => !Children.Any();

    public BitsFileViewModel(BitsFile node) : base(node) { }

    public override bool   CanOpenPreview => IsEmptyFile;
    public override string StatusText     => $"File: {Name}";
    public override string Icon           => "📄";
}

// ── Leaf ──────────────────────────────────────────────────────────────────────

public abstract class BitsLeafViewModel(BitsLeaf node) : BitsComponentViewModel(node)
{
    private static readonly ObservableCollection<BitsComponentViewModel> _empty = [];
    public override ObservableCollection<BitsComponentViewModel> Children => _empty;

    public override void CollapseAll() { }
    public override void RestoreExpansion(HashSet<string> expandedPaths) { }
    public override IEnumerable<string> ExpandedPaths() => [];
    public override void ForEach(Action<BitsComponentViewModel> action) => action(this);
}

public class BitsTemplateViewModel : BitsLeafViewModel
{
    private static readonly IBrush _color = new SolidColorBrush(Color.Parse("#cba6f7"));

    public BitsTemplateViewModel(BitsTemplate node) : base(node)
    {
        NodeColor = _color;
    }

    public override bool   CanIdentify    => true;
    public override bool   CanOpenPreview => true;
    public override string StatusText     => $"Template: {((BitsTemplate)Node).TemplateName}";
    public override string Icon           => "📜";
    public override void   ApplyColor()   => NodeColor = _color;
}

public class BitsRawFileViewModel : BitsLeafViewModel
{
    private static readonly IBrush _readableColor = new SolidColorBrush(Color.Parse("#7f849c"));
    private static readonly IBrush _binaryColor   = new SolidColorBrush(Color.Parse("#ff0000")) { Opacity = 0.5 };

    public bool IsBinaryRawFile { get; }

    public BitsRawFileViewModel(BitsRawFile node) : base(node)
    {
        IsBinaryRawFile = !ReadableExtensions.Contains(Path.GetExtension(node.Name));
        ApplyColor();
    }

    public override bool   CanOpenPreview => !IsBinaryRawFile;
    public override string StatusText     => $"File: {Name}";
    public override string Icon           => RawFileIcon(Node.Name);

    public override void ApplyColor() => NodeColor =
        IsBinaryRawFile                                                                    ? _binaryColor :
        Path.GetExtension(Node.Name).Equals(".skrit", StringComparison.OrdinalIgnoreCase) ? DefaultColor :
                                                                                             _readableColor;

    private static string RawFileIcon(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".tga" or ".dds" or ".bmp" or ".raw" => "🖼",
            ".wav" or ".mp3" or ".ogg" or ".aiff"                               => "🔊",
            ".skrit"                                                             => "📝",
            ".sno"                                                               => "📦",
            _                                                                    => "📎"
        };
}
