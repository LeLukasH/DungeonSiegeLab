# Príručka DependencyFinder — Interpreter vzor

Táto príručka opisuje Interpreter vzor, ktorý sa používa v implementácii `DependencyFinder`.

Hlavný účel: Analyzovať GAS šablóny a identifikovať všetky ich závislosti (textúry, zvuky, efekty, scripty, komponenty) bez veľkého `switch` bloku. Namiesto toho používame stromovú štruktúru výrazov, kde každé pravidlo je samostatná trieda. 


---

## Kde sú komponenty Interpreter

### Hlavný orchestrátor

- `src/Services/DependencyFinder.cs` — verejné API a orchestrácia

`DependencyFinder` je zodpovedný za:

- Načítanie konfigurácie pravidiel
- Parsovanie zdrojového kódu šablóny
- Vytvorenie stromu interpretátora
- Extrakciu lokálnych závislostí
- Rekurzívne riešenie dedičnosti cez `specializes`
- Deduplikáciu finálneho výsledku

### Interpreter typy

Všetky sú v priečinku `src/Services/DependencyFinder/Interpreter/`:

- `DependencyFinder.IExpression.cs` — rozhranie všetkých výrazov
- `DependencyFinder.TerminalExpression.cs` — abstraktná trieda listových pravidiel
- `DependencyFinder.NonterminalExpression.cs` — zložený výraz s deťmi
- `DependencyFinder.DependencyInterpretContext.cs` — kontext na interpretovanie jedného priradenia
- `DependencyFinder.AssignmentInterpreterFactory.cs` — fabrika, ktorá zloží strom
- `DependencyFinder.ConcreteExpressions.cs` — všetky konkrétne pravidlá (14 tried)

### Modely pre parsované priradenia

- `DependencyFinder.AssignmentRecord.cs` — jedno priradenie `path:key = value`
- `DependencyFinder.ParseResult.cs` — výsledok parsování šablóny
- `DependencyFinder.AnalyzeResult.cs` — výsledok analýzy s dedičnosťou

---

## Čo Interpreter vyhodnocuje

Vstup sú priradenia v GAS syntaxi, ktoré boli parsované zo `BitsTemplate.SourceCode`.

Príklady priradení:

```gas
specializes = base_template;
textures:0 = b_c_gah_helmet_01;
effect_script = enchant_fire;
item_1 = some_template;
common:instance_triggers { action_0 = "call_sfx_script(my_sound)"; }
```

Každé priradenie sa stane `AssignmentRecord` s:

- `Path` — momentálna cesta bloku, napríklad `aspect`, `inventory`, alebo `magic:enchantments`
- `Key` — normalizovaný kľúč priradenia
- `Value` — hodnota priradenia
- `Line` — číslo riadku v zdroji
- `Signature` — úplný podpis pre override kontrolu, zvyčajne `path:key`

Interpreter rozhoduje, či priradenie predstavuje jednu alebo viac `DependencyReference` objektov.

---

## Interpreter vzor v kóde

### Klúčové role

#### Rozhranie výrazu (`IExpression`)

```csharp
private interface IExpression
{
    void Interpret(DependencyInterpretContext context);
}
```

Všetky výrazy (terminálne aj neterminálne) implementujú toto rozhranie.

#### Terminálne výrazy (`TerminalExpression`)

```csharp
private abstract class TerminalExpression : IExpression
{
    public abstract void Interpret(DependencyInterpretContext context);
}
```

Abstraktná trieda pre konkrétne listové pravidlá. Každá podtrida predstavuje jedno alebo súvisiace pravidlá.

#### Konkrétne pravidlá (v `ConcreteExpressions.cs`)

Príklady:

- `SpecializesExpression` — detekuje `specializes = ...`
- `AspectTexturesExpression` — textúry v komponente `aspect`
- `InventoryExpression` — sloty a rozsahy inventára
- `CommonTriggerExpression` — funkcie triggerov (`call_sfx_script`, `has_go_in_inventory`, atď.)

Celkovo je 14 konkrétnych terminálov.

#### Neterminálny výraz (`NonterminalExpression`)

```csharp
private sealed class NonterminalExpression : IExpression
{
    private readonly IReadOnlyList<IExpression> _children;

    public void Interpret(DependencyInterpretContext context)
    {
        foreach (var child in _children)
            child.Interpret(context);
    }
}
```

Zloží všetky listové výrazy a vykoná ich postupne pre dané priradenie.

#### Kontext (`DependencyInterpretContext`)

```csharp
private sealed class DependencyInterpretContext
{
    public required string TemplateName { get; init; }
    public required List<DependencyReference> Dependencies { get; init; }
    public required AssignmentRecord Assignment { get; init; }
}
```

Nosí stav potrebný pre interpretovanie jedného priradenia. Každé pravidlo dostane kontext a rozhodne, či sa vzťahuje na priradenie.

#### Fabrika (`AssignmentInterpreterFactory`)

```csharp
private static class AssignmentInterpreterFactory
{
    public static IExpression Create(
        IReadOnlyDictionary<string, DependencyKind> fixedPropertyRules,
        ISet<string> inventoryDependencySlots)
        => new NonterminalExpression(
            new FixedPropertyExpression(fixedPropertyRules),
            new SpecializesExpression(),
            new AspectTexturesExpression(),
            // ... ďalšie pravidlá ...
        );
}
```

Fabrika vytvorí neterminálny koreň so všetkými listovými výrazmi. Používa sa raz v konštruktore `DependencyFinder`:

```csharp
_assignmentInterpreter = BuildAssignmentInterpreter();
```

---

## Ako sa Identify vykonáva

### Príklad: Pravidlo `SpecializesExpression`

```csharp
private sealed class SpecializesExpression : TerminalExpression
{
    public override void Interpret(DependencyInterpretContext context)
    {
        var a = context.Assignment;
        if (a.Key.Equals("specializes", StringComparison.OrdinalIgnoreCase))
            AddTokens(context.Dependencies, context.TemplateName, a, 
                DependencyKind.Template, "specializes", a.Value);
    }
}
```

Toto pravidlo:

1. Skontroluje, či kľúč je `"specializes"`
2. Ak áno, získa hodnotu (názov rodičovskej šablóny)
3. Pridá `DependencyReference` do výsledku

### Pipeline vykonávania

1. UI volá `ProjectBrowserViewModel.Identify()`
2. To volá `DependencyFinder.IdentifyDependencies(template, templateIndex)`
3. `IdentifyDependencies` spustí `AnalyzeTemplateRecursive`:
   - Parsuje šablónu → `ParseResult`
   - Extrahuje lokálne závislosti
   - Rekurzívne spracuje `specializes` rodičov
   - Deduplikuje výsledok
4. `ExtractLocalDependencies` pre každé priradenie:
   - Vytvorí `DependencyInterpretContext`
   - Spustí `_assignmentInterpreter.Interpret(context)`
   - `NonterminalExpression` iteruje cez potomkov
   - Každé pravidlo sa rozhodne, či pridá `DependencyReference`
5. Vrátia sa závislosti, UI aktualizuje panel a emituje event

### Príklad toku pre konkrétne priradenie

Ak v šablóne máme:

```gas
[aspect]
{
  textures:0 = b_my_texture_01;
  model = m_my_model;
}
```

Potom:

1. Parsovanie: Dve priradenia — `aspect:textures:0` a `aspect:model`
2. Pre `aspect:textures:0`:
   - `AspectTexturesExpression.Interpret()` detekuje pravidlo
   - Pridá `DependencyReference { Value = "b_my_texture_01", Kind = Texture, ... }`
3. Pre `aspect:model`:
   - Ak niet explicitného `aspect:textures`, inferencie sa aplikuje neskôr
   - Ale priamo sa nekonvertuje na textúru v Interpret

---

## Dedičnosť a preobraty

Ak šablóna má `specializes = parent_template`, analyzer:

1. Rekurzívne spracuje rodiča
2. Preberie jeho závislosti
3. Ale neberie závislosti, ktoré lokálne prekonali svoj podpis (override)

Príklad:

```gas
// parent_template
specializes = grandparent;
textures:0 = parent_texture;

// child_template  
specializes = parent_template;
textures:0 = child_texture;  // lokálny override
```

Keď analyzuješ `child_template`:

- `child_texture` je lokálna
- `parent_texture` z rodiča bude filtrovaná (pretože `child` má rovnaký podpis)
- Dedia sa iba nezneplatnené závislosti

---

## Konfigurácia pravidiel

Pravidlá sa načítavajú z `dependency-rules.json` v koreňovom adresári aplikácie. Ak súbor chýba alebo je neplatný, používajú sa defaults z `DependencyRulesConfig.CreateDefault()`.

Konfigurácia obsahuje:

- `VanillaBlocks` — komponenty, ktoré sa ignorujú ako custom
- `InventoryDependencySlots` — sloty inventára, ktoré referencujú šablóny
- `FixedPropertyKinds` — mapovanie `root:property` → `DependencyKind`

Príklad:

```json
{
  "VanillaBlocks": ["actor", "aspect", "attack", "body", ...],
  "InventoryDependencySlots": ["il_main", "es_head", ...],
  "FixedPropertyKinds": {
    "actor:portrait_icon": "Texture",
    "gui:inventory_icon": "Texture",
    "common:membership": "Template"
  }
}
```
