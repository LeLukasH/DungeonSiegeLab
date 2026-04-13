namespace DungeonSiegeLab.Models;

public enum DependencyKind
{
    Template,
    Texture,
    Sound,
    Effect,
    Script,
    Component,
    Other
}

public class DependencyReference
{
    public string Value { get; init; } = "";
    public DependencyKind Kind { get; init; } = DependencyKind.Other;
    public string Rule { get; init; } = "";
    public string SourcePath { get; init; } = "";
    public int Line { get; init; }
    public bool IsInherited { get; init; }
    public string SourceTemplate { get; init; } = "";

    // Signature of source assignment (path + property) used for override checks in inheritance.
    public string SourceSignature { get; init; } = "";
}
