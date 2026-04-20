# Identify Logic Design Patterns

This document lists design patterns that are present in the Identify dependency flow.

## Scope

The analysis focuses on the Identify pipeline in:
- src/ViewModels/ProjectBrowserViewModel.cs
- src/Services/DependencyFinder.cs
- src/Models/DependencyReference.cs

## Patterns Found

## 1. Iterator / Enumeration

Why it applies:
- The logic repeatedly traverses dependency collections and node collections with foreach and LINQ projections.

Evidence:
- src/ViewModels/ProjectBrowserViewModel.cs (dependency grouping and summaries)
- src/ViewModels/ProjectBrowserViewModel.cs (copying dependency list into observable state)
- src/Services/DependencyFinder.cs (iterating parsed assignments and emitted dependencies)

## 2. Command

Why it applies:
- Identify is implemented as a command method and triggered from the view by command binding.

Evidence:
- src/ViewModels/ProjectBrowserViewModel.cs (Identify method marked with RelayCommand)
- src/Views/ProjectBrowserView.axaml (Identify button bound to IdentifyCommand)

## 3. Observer

Why it applies:
- ViewModel emits events when identify data is produced, allowing other consumers to subscribe without tight coupling.

Evidence:
- src/ViewModels/ProjectBrowserViewModel.cs (DependenciesIdentified event)
- src/ViewModels/ProjectBrowserViewModel.cs (TexturesIdentified event)
- src/ViewModels/ProjectBrowserViewModel.cs (DependenciesIdentified invocation)

## 4. Composite Traversal

Why it applies:
- Template index creation recursively walks a tree of nodes and children.

Evidence:
- src/ViewModels/ProjectBrowserViewModel.cs (BuildTemplateIndex)
- src/ViewModels/ProjectBrowserViewModel.cs (CollectTemplates recursive walk)

## 5. Interpreter / Parser

Why it applies:
- GAS-like text is parsed into structured assignments and then interpreted into semantic dependency references.

Evidence:
- src/Services/DependencyFinder.cs (ParseTemplate)
- src/Services/DependencyFinder.cs (ExtractLocalDependencies)

## 6. Rule Engine (Chain-like rule evaluation)

Why it applies:
- Dependency extraction applies many independent rule checks in sequence, each capable of emitting results.

Evidence:
- src/Services/DependencyFinder.cs (fixed property rule mapping)
- src/Services/DependencyFinder.cs (ApplyCommonTriggerRules)
- src/Services/DependencyFinder.cs (ApplyInventoryRules)
- src/Services/DependencyFinder.cs (implicit texture inference)

## 9. Adapter Mapping

Why it applies:
- DependencyReference texture entries are mapped into TextureReference so existing Texture Lab flow remains compatible.

Evidence:
- src/ViewModels/ProjectBrowserViewModel.cs (dependencies filtered and projected to TextureReference)

