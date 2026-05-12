using System.Collections.ObjectModel;

namespace DungeonSiegeLab.Models;

public abstract class BitsComponent
{
    public string Name      { get; init; } = "";
    public string FullPath  { get; init; } = "";
    public BitsComponent? Parent { get; set; }

    public abstract void ForEach(Action<BitsComponent> action);
    public abstract IEnumerable<T> FindAll<T>() where T : BitsComponent;

    public override string ToString() => Name;
}

/// <summary>Base for nodes that contain children (folder, .gas file).</summary>
public abstract class BitsComposite : BitsComponent
{
    public ObservableCollection<BitsComponent> Children { get; } = [];

    public override void ForEach(Action<BitsComponent> action)
    {
        action(this);
        foreach (var child in Children)
            child.ForEach(action);
    }

    public override IEnumerable<T> FindAll<T>()
    {
        if (this is T self) yield return self;
        foreach (var child in Children)
            foreach (var found in child.FindAll<T>())
                yield return found;
    }
}

/// <summary>Priečinok v /Bits - môže obsahovať ďalšie priečinky a súbory.</summary>
public class BitsFolder : BitsComposite { }

/// <summary>.gas súbor - môže obsahovať viacero templates.</summary>
public class BitsFile : BitsComposite { }

/// <summary>Base for nodes that have no children (template, raw file).</summary>
public abstract class BitsLeaf : BitsComponent
{
    public override void ForEach(Action<BitsComponent> action) => action(this);

    public override IEnumerable<T> FindAll<T>()
    {
        if (this is T self) yield return self;
    }
}

/// <summary>Jeden template definovaný v .gas súbore.</summary>
public class BitsTemplate : BitsLeaf
{
    public string TemplateName { get; init; } = "";
    public string SourceCode   { get; init; } = "";
}

/// <summary>Non-.gas file (image, sound, script, mesh, etc.) — content loaded on demand.</summary>
public class BitsRawFile : BitsLeaf { }
