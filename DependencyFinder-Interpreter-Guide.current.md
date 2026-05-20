# DependencyFinder Patterns Guide

This guide describes the patterns used by the current `DependencyFinder` implementation.

It focuses on two patterns:

1. **Interpreter pattern**
2. **Enumeration Method pattern**

The current implementation uses the Interpreter pattern for assignment-rule evaluation and uses `EnumerationUtility.Enumerate(...)` directly for traversal. 

---

## Where These Patterns Live

### Core orchestrator

- `src/Services/DependencyFinder.cs`

`DependencyFinder` is responsible for orchestration:

- loading dependency rules
- parsing template source text
- building the assignment interpreter
- extracting local dependencies
- recursively resolving inherited dependencies through `specializes`
- deduplicating the final result

The concrete assignment-rule logic lives in terminal expression classes.

### Interpreter support types

- `src/Services/DependencyFinder/Interpreter/DependencyFinder.IExpression.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.TerminalExpression.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.NonterminalExpression.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.DependencyInterpretContext.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.AssignmentInterpreterFactory.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.ConcreteExpressions.cs`

### Parsed language model types

- `src/Services/DependencyFinder/Interpreter/DependencyFinder.AssignmentRecord.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.ParseResult.cs`
- `src/Services/DependencyFinder/Interpreter/DependencyFinder.AnalyzeResult.cs`

---

## What The Interpreter Is Interpreting

The interpreted input is assignment-level template syntax parsed from `BitsTemplate.SourceCode`.

Examples of parsed assignments:

```gas
specializes = base_template;
textures:0 = b_c_gah_helmet_01;
effect_script = enchant_fire;
item_1 = some_template;
```

Each parsed assignment becomes an `AssignmentRecord` with:

- `Path` — current block path, for example `aspect`, `inventory`, or `magic:enchantments`
- `Key` — normalized assignment key
- `Value` — raw assignment value
- `Line` — source line number
- `Signature` — full local override signature, usually `path:key`

The interpreter decides whether an assignment implies one or more `DependencyReference` objects.

Examples of dependency meanings:

- template dependency
- texture dependency
- sound dependency
- script dependency
- effect dependency
- component dependency

---

## Interpreter Pattern In This Code

The Interpreter pattern is implemented as assignment-rule evaluation.

### Roles

#### Context

`DependencyInterpretContext`

Contains the state needed to interpret one assignment:

- `TemplateName`
- `Dependencies`
- `Assignment`

Concrete expressions receive all per-assignment state through the context and use shared static helper methods on the partial `DependencyFinder` class.

#### Abstract expression

`IExpression`

```csharp
private interface IExpression
{
    void Interpret(DependencyInterpretContext context);
}
```

Every interpreter expression implements this contract.

#### Terminal expression base

`TerminalExpression`

```csharp
private abstract class TerminalExpression : IExpression
{
    public abstract void Interpret(DependencyInterpretContext context);
}
```

This is the abstract base class for concrete leaf rules. It is no longer a wrapper around an inline function.

#### Concrete terminal expressions / leaf expressions

Concrete leaf expressions live in `DependencyFinder.ConcreteExpressions.cs`.

Current concrete terminal expressions:

- `FixedPropertyExpression`
- `SpecializesExpression`
- `AspectTexturesExpression`
- `AspectVoiceExpression`
- `AspectVoVoiceExpression`
- `ConversationExpression`
- `CommonTriggerExpression`
- `InventoryExpression`
- `GoldRangeExpression`
- `MagicEnchantmentExpression`
- `MindJatExpression`
- `PContentExpression`
- `PhysicsBreakParticulateExpression`
- `PotionRangeExpression`
- `StoreItemRestockExpression`

Each class owns one dependency rule or one closely related rule group.

For example:

- `SpecializesExpression` handles `specializes = ...`
- `AspectTexturesExpression` handles `aspect` texture assignments
- `InventoryExpression` handles inventory slot dependencies and inventory range dependencies
- `CommonTriggerExpression` handles trigger action and condition function arguments
- `MagicEnchantmentExpression` handles enchantment effect scripts

This keeps rule logic out of `DependencyFinder` and makes the leaf expressions closer to concrete Interpreter-pattern classes.

#### Nonterminal expression

`NonterminalExpression`

```csharp
private sealed class NonterminalExpression : IExpression
{
    private readonly IReadOnlyList<IExpression> _children;

    public NonterminalExpression(params IExpression[] children)
    {
        _children = children;
    }

    public void Interpret(DependencyInterpretContext context)
    {
        foreach (var child in _children)
            child.Interpret(context);
    }
}
```

`NonterminalExpression` composes the leaf expressions and executes them in sequence for the current assignment.

#### Interpreter factory

`AssignmentInterpreterFactory`

The factory builds the concrete expression tree:

```csharp
AssignmentInterpreterFactory.Create(_fixedPropertyRules, _inventoryDependencySlots)
```

It returns a `NonterminalExpression` that contains all concrete terminal expressions.

`DependencyFinder` only calls the factory through:

```csharp
private IExpression BuildAssignmentInterpreter()
    => AssignmentInterpreterFactory.Create(_fixedPropertyRules, _inventoryDependencySlots);
```

---

## Execution Pipeline

1. `DependencyFinder` is constructed.
2. It loads dependency rules.
3. It creates `_assignmentInterpreter` by calling `BuildAssignmentInterpreter()`.
4. `IdentifyDependencies(...)` starts dependency analysis.
5. `AnalyzeTemplateRecursive(...)` parses the current template and resolves inherited dependencies.
6. `ParseTemplate(...)` converts source text into `ParseResult` and `AssignmentRecord` objects.
7. `ExtractLocalDependencies(...)` adds non-vanilla block dependencies.
8. `ExtractLocalDependencies(...)` directly calls `EnumerationUtility.Enumerate(parsed.Assignments, ...)`.
9. For each assignment, it creates a `DependencyInterpretContext`.
10. `_assignmentInterpreter.Interpret(context)` is called.
11. `NonterminalExpression` forwards the context to every concrete terminal expression.
12. Matching terminal expressions append `DependencyReference` objects.
13. If no explicit `aspect:textures` assignment exists, `aspect:model` may produce an inferred texture dependency.
14. Dependencies are deduplicated and returned.
15. During inheritance resolution, `AnalyzeTemplateRecursive(...)` directly calls `EnumerationUtility.Enumerate(parent.Dependencies, ...)` to copy inherited dependencies unless overridden by local signatures.

---

## Enumeration Method Pattern In This Code

Enumeration Method means traversal is centralized and caller behavior is passed in as an action.

The current code uses this pattern by calling `EnumerationUtility.Enumerate(...)` directly.

### Current direct usages

#### Assignment traversal

Inside `ExtractLocalDependencies(...)`:

```csharp
EnumerationUtility.Enumerate(parsed.Assignments, a =>
{
    _assignmentInterpreter.Interpret(new DependencyInterpretContext
    {
        TemplateName = template.TemplateName,
        Dependencies = dependencies,
        Assignment = a
    });
});
```

This walks parsed assignments and lets the interpreter decide what each assignment means.

#### Inherited dependency traversal

Inside `AnalyzeTemplateRecursive(...)`:

```csharp
EnumerationUtility.Enumerate(parent.Dependencies, dep =>
{
    if (!string.IsNullOrEmpty(dep.SourceSignature)
        && parsed.LocalSignatures.Contains(dep.SourceSignature))
    {
        return;
    }

    combined.Dependencies.Add(new DependencyReference
    {
        Value = dep.Value,
        Kind = dep.Kind,
        Rule = dep.Rule,
        SourcePath = dep.SourcePath,
        Line = dep.Line,
        IsInherited = true,
        SourceTemplate = dep.SourceTemplate,
        SourceSignature = dep.SourceSignature
    });
});
```

This walks parent dependencies and copies only dependencies that are not locally overridden.

---

## How Both Patterns Work Together

The design has two layers:

1. **Enumeration layer**
   - `EnumerationUtility.Enumerate(...)` controls traversal.
   - It is called directly from `DependencyFinder`.

2. **Interpreter layer**
   - `IExpression` defines the interpreter contract.
   - `NonterminalExpression` composes the grammar/rule list.
   - Concrete `TerminalExpression` classes interpret individual assignment rules.

Together:

- traversal remains centralized and reusable
- assignment meaning is isolated in concrete leaf classes
- `DependencyFinder` remains an orchestrator
- adding a new dependency rule usually means adding a new terminal expression and registering it in `AssignmentInterpreterFactory`

---

## Current Design Summary

`DependencyFinder` should do orchestration, not rule interpretation.

Concrete terminal expressions should do rule interpretation.

`AssignmentInterpreterFactory` should assemble the expression tree.

`NonterminalExpression` should run the leaf expressions.

`EnumerationUtility.Enumerate(...)` should be called directly where traversal is needed.

---

## Class Diagram

![DependencyFinder class diagram](dependency_class_diagram.svg)
