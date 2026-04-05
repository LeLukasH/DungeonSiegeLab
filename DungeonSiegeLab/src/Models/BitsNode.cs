using System.Collections.ObjectModel;

namespace DungeonSiegeLab.Models;

// ============================================================
// COMPOSITE PATTERN
// BitsNode je abstraktný komponent. BitsFolder je composite
// (môže mať deti), BitsFile a BitsTemplate sú listy.
// ============================================================

public abstract class BitsNode
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public BitsNode? Parent { get; set; }

    public abstract bool IsLeaf { get; }

    public override string ToString() => Name;
}

/// <summary>Priečinok v /Bits - môže obsahovať ďalšie priečinky a súbory.</summary>
public class BitsFolder : BitsNode
{
    public ObservableCollection<BitsNode> Children { get; } = new();
    public override bool IsLeaf => Children.Count == 0;
}

/// <summary>.gas súbor - môže obsahovať viacero templates.</summary>
public class BitsFile : BitsNode
{
    public ObservableCollection<BitsNode> Children { get; } = new();
    public override bool IsLeaf => Children.Count == 0;
}

/// <summary>Jeden template definovaný v .gas súbore.</summary>
public class BitsTemplate : BitsNode
{
    public string TemplateName { get; init; } = "";
    public string SourceCode { get; init; } = "";
    public override bool IsLeaf => true;
}
