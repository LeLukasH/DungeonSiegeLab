# Class Diagrams


- `facade.txt`
- `strategy.txt`
- `singletons.txt`


## 1. Facade

### Prečo je `RawTextureConverter` fasáda

Trieda [`RawTextureConverter`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs) vystupuje ako hlavný vstupný bod pre prácu s textúrami. Navonok ponúka jednoduché metódy:

- `LoadTextureAsync(...)` na riadkoch 28-34
- `LoadFromPathAsync(...)` na riadkoch 36-52
- `SaveAsAsync(...)` na riadkoch 54-61
- `SaveToProjectAsync(...)` na riadkoch 63-75
- `ImportReplacementAsync(...)` na riadkoch 77-80

To znamená, že klient nemusí riešiť vnútorné detaily konverzie a práce s formátmi. Stačí mu volať jednu triedu.

### Čo táto fasáda skrýva

`RawTextureConverter` v sebe skrýva viacero vnútorných krokov:

- výber exportnej stratégie cez [`TextureExportStrategyFactory`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/TextureExportStrategyFactory.cs), konkrétne na riadkoch 20-25 a 59-60 v `RawTextureConverter`
- vytvorenie pomocných závislostí pre stratégie cez [`TextureExportDependencies`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/TextureExportDependencies.cs), konkrétne na riadkoch 14-18 v `RawTextureConverter`
- volanie externých nástrojov cez [`ExternalTextureToolService`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/ExternalTextureToolService.cs), napríklad na riadkoch 101-102, 125-126 a 155-157 v `RawTextureConverter`
- prípravu preview pipeline pre `RAW`, `PSD` a `PNG` na riadkoch 82-110
- prípravu pracovného PSD na riadkoch 112-146

Preto je popis fasády správny:

- poskytuje zjednodušené API pre načítanie, import a export textúr
- skrýva export workflow
- skrýva volanie `RawToPsd.exe` a `PsdToRaw.exe`
- skrýva výber konkrétnej stratégie exportu

### Ako sedí Facade diagram s kódom

Vzťahy z diagramu zodpovedajú týmto miestam:

- `RawTextureConverter --> ExternalTextureToolService : uses`
  Potvrdzuje pole `_toolService` na riadku 8 a jeho použitie napríklad na riadkoch 101, 125 a 156 v [`RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)

- `RawTextureConverter --> TextureExportStrategyFactory : uses`
  Potvrdzuje pole `_exportStrategyFactory` na riadku 9, vytvorenie na riadkoch 20-25 a použitie na riadkoch 59-60 v [`RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)

- `RawTextureConverter --> TextureExportDependencies : creates`
  Potvrdzuje pole `_exportDependencies` na riadku 10 a vytvorenie `new TextureExportDependencies` na riadkoch 14-18 v [`RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)

- `RawTextureConverter ..> ITextureExportStrategy : calls ExportAsync`
  Potvrdzuje získanie stratégie cez `GetStrategy(...)` na riadku 59 a volanie `strategy.ExportAsync(...)` na riadku 60 v [`RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)

- `TextureExportStrategyFactory --> ITextureExportStrategy : selects / returns`
  Potvrdzuje slovník `_strategies` na riadku 7 a metóda `GetStrategy(...)` na riadkoch 14-20 v [`TextureExportStrategyFactory.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/TextureExportStrategyFactory.cs)

## 2. Strategy

### Prečo používame Strategy pattern

Export do rôznych formátov už nie je riešený jedným veľkým `switch` blokom. Namiesto toho máme spoločné rozhranie a viacero konkrétnych algoritmov exportu.

Rozhranie je:

- [`ITextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/ITextureExportStrategy.cs), riadky 5-10

Konkrétne stratégie sú:

- [`PngTextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PngTextureExportStrategy.cs), riadky 5-20
- [`PsdTextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PsdTextureExportStrategy.cs), riadky 5-18
- [`RawTextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs), riadky 5-18

Každá stratégia implementuje rovnaké rozhranie, ale vykonáva iný algoritmus exportu:

- `PngTextureExportStrategy` kopíruje PNG preview z `PngCachePath`, riadky 13-18
- `PsdTextureExportStrategy` si vyžiada pracovné PSD cez dependencies a skopíruje ho, riadky 13-16
- `RawTextureExportStrategy` si vyžiada pracovné PSD a následne spustí RAW export cez dependencies, riadky 13-16

### Akú úlohu má factory

Výber správnej stratégie nerieši klient priamo. Na to slúži:

- [`TextureExportStrategyFactory`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/TextureExportStrategyFactory.cs)

Factory:

- prijme kolekciu stratégií v konštruktore, riadky 9-12
- uloží ich do slovníka podľa `TargetFormat`, riadok 11
- cez `GetStrategy(targetFormat)` vyberie správnu implementáciu, riadky 14-20

V [`RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs) sa potom táto factory použije v `SaveAsAsync(...)`:

- `GetStrategy(targetFormat)` na riadku 59
- `strategy.ExportAsync(...)` na riadku 60

### Akú úlohu majú `TextureExportDependencies`

Trieda [`TextureExportDependencies`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/TextureExportDependencies.cs) nesie pomocné operácie, ktoré stratégie potrebujú:

- `EnsureWorkingPsdAsync` na riadku 7
- `ExportRawAsync` na riadku 9

Vytvára ich [`RawTextureConverter`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs) na riadkoch 14-18.

To znamená:

- stratégie obsahujú exportný algoritmus
- helper operácie dostanú zvonka cez `TextureExportDependencies`

### Ako sedí Strategy diagram s kódom

Vzťahy z diagramu sú opreté o tieto miesta:

- `ITextureExportStrategy <|.. PngTextureExportStrategy`
  Potvrdzuje deklarácia `public class PngTextureExportStrategy : ITextureExportStrategy` na riadku 5 v [`PngTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PngTextureExportStrategy.cs)

- `ITextureExportStrategy <|.. PsdTextureExportStrategy`
  Potvrdzuje deklarácia na riadku 5 v [`PsdTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PsdTextureExportStrategy.cs)

- `ITextureExportStrategy <|.. RawTextureExportStrategy`
  Potvrdzuje deklarácia na riadku 5 v [`RawTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs)

- `TextureExportStrategyFactory --> ITextureExportStrategy : returns`
  Potvrdzuje návratový typ metódy `GetStrategy(...)` na riadku 14 v [`TextureExportStrategyFactory.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/TextureExportStrategyFactory.cs)

- `PsdTextureExportStrategy --> TextureExportDependencies : uses`
  Potvrdzuje parameter `TextureExportDependencies dependencies` na riadku 13 a použitie `dependencies.EnsureWorkingPsdAsync(...)` na riadku 15 v [`PsdTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PsdTextureExportStrategy.cs)

- `RawTextureExportStrategy --> TextureExportDependencies : uses`
  Potvrdzuje parameter `TextureExportDependencies dependencies` na riadku 13 a použitia na riadkoch 15-16 v [`RawTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs)

Poznámka:

`PngTextureExportStrategy` síce prijíma `TextureExportDependencies` v signatúre metódy, ale v tele ich momentálne aktívne nepoužíva. Preto v diagrame nie je nutné kresliť šípku `PngTextureExportStrategy --> TextureExportDependencies`.

## 3. Singleton

### Prečo môžeme hovoriť o singletonoch

Všetky tri export stratégie sú implementované ako singletony. Každá trieda má:

- verejnú statickú inštanciu `Instance`
- privátny konštruktor

To znamená, že:

- zvonka nie je možné vytvárať nové inštancie cez `new`
- aplikácia používa jednu zdieľanú inštanciu každej stratégie

### Konkrétne dôkazy v kóde

[`PngTextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PngTextureExportStrategy.cs):

- `public static readonly PngTextureExportStrategy Instance = new();` na riadku 7
- `private PngTextureExportStrategy() { }` na riadku 9

[`PsdTextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/PsdTextureExportStrategy.cs):

- `public static readonly PsdTextureExportStrategy Instance = new();` na riadku 7
- `private PsdTextureExportStrategy() { }` na riadku 9

[`RawTextureExportStrategy`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs):

- `public static readonly RawTextureExportStrategy Instance = new();` na riadku 7
- `private RawTextureExportStrategy() { }` na riadku 9

Použitie týchto singletonov v praxi potvrdzuje [`RawTextureConverter`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs), kde sa factory skladá takto:

- `PngTextureExportStrategy.Instance` na riadku 22
- `PsdTextureExportStrategy.Instance` na riadku 23
- `RawTextureExportStrategy.Instance` na riadku 24

### Ako sedí Singleton diagram s kódom

Každá stratégia v diagrame obsahuje:

- `{static} +Instance : ConcreteStrategy`
- `-ConcreteStrategy()`

To presne zodpovedá nášmu kódu. Diagram teda nevymýšľa nič navyše, ale priamo mapuje:

- statickú inštanciu
- privátny konštruktor
- implementáciu rozhrania `ITextureExportStrategy`

## 4. Externé nástroje a väzba na diagramy

Trieda [`ExternalTextureToolService`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/ExternalTextureToolService.cs) rieši:

- `ConvertRawToPsdAsync(...)` na riadkoch 7-20
- `ConvertPsdToRawAsync(...)` na riadkoch 22-35

Volanie toolov prebieha cez `RunToolAsync(...)` na riadkoch 44-72.

Bundled cesty k týmto nástrojom sú centralizované v:

- [`BundledToolPaths`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/BundledToolPaths.cs), riadky 3-12

Konkrétne:

- `RawToPsdPath` na riadkoch 8-9
- `PsdToRawPath` na riadkoch 11-12

To podporuje najmä Facade diagram, pretože `RawTextureConverter` tieto detaily skrýva pred zvyškom aplikácie.

## 4. Interpreter

### Prečo používame Interpreter pattern

Funkcia Identify analyzuje GAS súbory a identifikuje všetky závislosti šablóny (textúry, zvuky, efekty, scripty, komponenty). Namiesto veľkého `switch` bloku s pravidlami pre všetky druhy vlastností, používame Interpreter pattern na vyhodnocovanie gramatiky priradení.

Jednotlivé pravidlá sú reprezentované ako výrazy stromovej štruktúry:

- abstraktné rozhranie: `IExpression`
- terminálne výrazy (listové uzly): `TerminalExpression` — konkrétne pravidlá
- neterminálne výrazy (zložené uzly): `NonterminalExpression` — sekvenčné vykonávanie

Tento prístup umožňuje:

- ľahké pridávanie nových pravidiel bez modifikácie orchestrátora
- čitateľnosť a údržbu — každé pravidlo je v samostatnej triede
- kompozíciu komplexných logík z jednoduchých kusov

### Kde sú Interpreter komponenty

Jadro orchestrácie:

- [`DependencyFinder`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder.cs) — hlavný vstupný bod pre analýzu závislostí

Interpreter podpora — typy v priečinku `DependencyFinder/Interpreter/`:

- [`DependencyFinder.IExpression.cs`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.IExpression.cs) — rozhranie všetkých výrazov
- [`DependencyFinder.TerminalExpression.cs`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.TerminalExpression.cs) — abstraktná trieda listových pravidiel
- [`DependencyFinder.NonterminalExpression.cs`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.NonterminalExpression.cs) — zložený výraz s deťmi
- [`DependencyFinder.DependencyInterpretContext.cs`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.DependencyInterpretContext.cs) — kontext obsahujúci stav na interpretovanie jedného priradenia
- [`DependencyFinder.AssignmentInterpreterFactory.cs`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.AssignmentInterpreterFactory.cs) — fabrika, ktorá zloží strom výrazov

Konkrétne terminálne výrazy — pravidlá v jednom súbore:

- [`DependencyFinder.ConcreteExpressions.cs`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.ConcreteExpressions.cs)

Aktuálne konkrétne terminálne výrazy:

- `FixedPropertyExpression` — pravidlá z konfigurácie
- `SpecializesExpression` — odkaz na rodičovskú šablónu
- `AspectTexturesExpression` — textúry v komponente `aspect`
- `AspectVoiceExpression` — zvuky hlasu
- `AspectVoVoiceExpression` — VO zvuky
- `ConversationExpression` — templates v dialógoch
- `CommonTriggerExpression` — funkcie triggerov (`call_sfx_script`, `has_go_in_inventory`, atď.)
- `InventoryExpression` — sloty a rozsahy inventára
- `GoldRangeExpression` — rozsahy zlata
- `MagicEnchantmentExpression` — scriptu čarov
- `MindJatExpression` — AI scriptu (`jat_*`)
- `PContentExpression` — ikony a textúry v obsahu hráča
- `PhysicsBreakParticulateExpression` — efekty rozbitia
- `PotionRangeExpression` — rozsahy elixírov
- `StoreItemRestockExpression` — doplňovanie obchodov

### Ako sa Interpreter stromuje

Fabrika `AssignmentInterpreterFactory` vytvorí `NonterminalExpression` s detskými uzlami pre všetky konkrétne pravidlá:

```csharp
public static IExpression Create(
    IReadOnlyDictionary<string, DependencyKind> fixedPropertyRules,
    ISet<string> inventoryDependencySlots)
    => new NonterminalExpression(
        new FixedPropertyExpression(fixedPropertyRules),
        new SpecializesExpression(),
        new AspectTexturesExpression(),
        // ... ďalšie pravidlá ...
    );
```

`DependencyFinder` ju volá raz v konštruktore:

```csharp
_assignmentInterpreter = BuildAssignmentInterpreter();
```

### Ako sa vykoná interpretovanie

V metóde [`ExtractLocalDependencies`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder.cs), sa pre každé priradenie vytvorí [`DependencyInterpretContext`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder/Interpreter/DependencyFinder.DependencyInterpretContext.cs) a pošle sa stromom:

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

`NonterminalExpression.Interpret(...)` iteruje cez svojich potomkov a volá ich `Interpret(...)`:

```csharp
public void Interpret(DependencyInterpretContext context)
{
    foreach (var child in _children)
        child.Interpret(context);
}
```

Každé pravidlo (`TerminalExpression`) skontroluje, či sa jeho podmienka vzťahuje na priradenie, a ak áno, pripraví `DependencyReference` objekty.

Príklad — `SpecializesExpression`:

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

### Ako sedí Interpreter so zvyškom Identify

Orchestrácia v [`DependencyFinder`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/Services/DependencyFinder.cs):

1. **Verejné API** (`IdentifyDependencies`) — vstupný bod z UI
2. **Rekurzívne dedenie** (`AnalyzeTemplateRecursive`) — nasleduje `specializes` a zlučuje rodičovské závislosti
3. **Parsovanie** (`ParseTemplate`) — prevedie text na `AssignmentRecord` a `ParseResult`
4. **Lokálne závislosti** (`ExtractLocalDependencies`) — spustí Interpreter na priradeniach
5. **Deduplikácia** (`Deduplicate`) — unikátne závislosti podľa identity kľúča

UI volá identifikáciu z [`ProjectBrowserViewModel.Identify()`](C:/Users/elisk/Documents/GitHub/DungeonSiegeLab/src/ViewModels/ProjectBrowserViewModel.cs), ktorá:

- volá `IdentifyDependencies(...)`
- aktualizuje observovateľný stav `IdentifiedDependencies`
- otvára panel závislostí
- emituje event `DependenciesIdentified` pre poslucháčov

