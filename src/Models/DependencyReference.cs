namespace DungeonSiegeLab.Models;


/// Normalized dependency categories used by the Identify pipeline and frontend filters.
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

/// One resolved dependency occurrence discovered in template source.
public class DependencyReference
{
    /// Resolved token/value (template id, texture id, script path, etc.).
    /// Frontend primary label.
    public string Value { get; init; } = "";

    /// High-level dependency type for grouping and filtering.
    public DependencyKind Kind { get; init; } = DependencyKind.Other;

    /// Rule id that produced this dependency (used for diagnostics / inspector details).
    public string Rule { get; init; } = "";

    /// Logical GAS path where the dependency was found (component:property).
    /// Useful as secondary context text and path filter key.
    public string SourcePath { get; init; } = "";

    /// 1-based line inside template source when known, 0 for inferred values.
    /// Use with SourcePath to drive line markers and jump-to-source.
    public int Line { get; init; }

    /// True when dependency came from a superclass via specializes recursion.
    public bool IsInherited { get; init; }

    /// Template that originally produced this dependency.
    /// Useful for inherited dependency provenance in details panel.
    public string SourceTemplate { get; init; } = "";

    // Signature of source assignment (path + property) used for override checks in inheritance.
    public string SourceSignature { get; init; } = "";

    // Frontend-friendly helpers for quick labels/tooltips/chips.
    public string Origin => IsInherited ? "Inherited" : "Local";
    public string DisplayText => $"[{Kind}] {Value} ({Origin}) @ {SourcePath}";
}
