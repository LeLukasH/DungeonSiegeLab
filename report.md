# DungeonSiegeLab Report


Najdôležitejšie implementované oblasti:

- podpora otvárania `PNG`, `PSD` a `RAW` textúr v `TextureLab`
- jednotný `Save As...` flow pre export textúr do rôznych formátov
- integrácia bundled nástrojov `RawToPsd.exe` a `PsdToRaw.exe`
- preview pipeline pre `RAW` cez externý `RawToPsd.exe`
- spracovanie `PSD` a `PNG` cez `Magick.NET`
- bezpečný conversion flow cez dočasné súbory a dočasné priečinky
- export do projektovej štruktúry `/Bits`

## Zmeny v TextureLab

`TextureLab` už nie je navrhnutý len ako viewer pre jeden konkrétny formát, ale ako všeobecné pracovné okno pre textúry.

Aktuálne podporuje:

- otvorenie `.png`
- otvorenie `.psd`
- otvorenie `.raw`

UI zostalo konzistentné so zvyškom aplikácie:

- všetky texty sú v angličtine
- akcie majú krátke a jasné názvy
- toolbar a pravý info panel používajú rovnaký štýl ako ostatné časti aplikácie
- zachovali sme rozdelenie na `primary` a `secondary` tlačidlá

Hlavné akcie v `TextureLab` sú teraz:

- `Save to Project...`
- `Save As...`
- `Import Replacement...`

## Ako funguje otvorenie a náhľad RAW a PSD v TextureLab

Toto je jedna z najdôležitejších vecí, ktoré sme dnes zaviedli.

### Základná myšlienka

Samotné UI nepracuje priamo s `.raw` alebo `.psd` ako s formátmi na zobrazenie.

Namiesto toho sme spravili jednotný preview model:

- bez ohľadu na pôvodný formát si `TextureLab` pripraví dočasný PNG preview
- UI potom vždy zobrazuje PNG bitmapu

To znamená, že zobrazovacia vrstva je jednoduchá a nemusí riešiť zvlášť RAW a zvlášť PSD.

### Ako otvárame PSD

Keď používateľ otvorí `.psd`:

1. `TextureLabViewModel` zavolá konverznú službu
2. `RawTextureConverter` rozpozná, že vstupný formát je `PSD`
3. pomocou `Magick.NET` sa z PSD vytvorí dočasný PNG súbor
4. tento PNG sa uloží ako preview cache
5. `TextureTabViewModel` z neho vytvorí `Bitmap` a zobrazí ju v UI

Takže PSD sa síce otvorí ako vstupný formát, ale samotné zobrazenie v okne ide cez vygenerovaný PNG preview.

### Ako otvárame RAW

Keď používateľ otvorí `.raw`, je to o krok zložitejšie:

1. originálny `.raw` sa najprv skopíruje do dočasného priečinka
2. nad touto temp kópiou sa spustí `RawToPsd.exe`
3. tool vytvorí `.psd` s rovnakým názvom ako vstupný `.raw`
4. toto dočasné PSD sa následne načíta cez `Magick.NET`
5. z PSD sa vytvorí dočasný PNG preview
6. ten sa zobrazí v `TextureLab`

Tento návrh sme zvolili preto, aby:

- sme nemenili originálne súbory v projekte len kvôli preview
- sme neprodukovali pomocné PSD súbory vedľa reálnych assetov
- sme mali jednotnú zobrazovaciu pipeline aj pre RAW

### Výsledok

Používateľ v `TextureLab` vidí:

- `RAW`
- `PSD`
- `PNG`

vždy rovnakým spôsobom, hoci interná cesta k preview je pre každý formát trochu iná.

## Bundled nástroje v projekte

Zrušili sme potrebu, aby používateľ nastavoval cesty k externým toolom ručne.

Namiesto toho sme im vyhradili pevné miesto v projekte:

- `src/Tools/RawToPsd/RawToPsd.exe`
- `src/Tools/PsdToRaw/PsdToRaw.exe`

Tieto súbory sa kopírujú do build výstupu automaticky cez `.csproj`, takže aplikácia ich vie nájsť bez ďalšej konfigurácie.

To zjednodušuje používanie aplikácie a znižuje počet miest, kde môže vzniknúť chyba.

## Ako funguje konverzia

### Otvorenie RAW

Pri otvorení RAW prebieha tento flow:

1. kopírovanie RAW do temp priečinka
2. spustenie `RawToPsd.exe`
3. vznik dočasného PSD
4. konverzia PSD do PNG cez `Magick.NET`
5. zobrazenie PNG preview

### Otvorenie PSD

Pri otvorení PSD:

1. PSD sa načíta
2. cez `Magick.NET` sa z neho vytvorí preview PNG
3. preview sa zobrazí v UI

### Otvorenie PNG

Pri otvorení PNG:

1. PNG sa skopíruje ako preview cache
2. zobrazí sa priamo v UI

## Save As flow

Nový `Save As...` je riadený podľa cieľového formátu.

Cieľový formát sa určuje podľa prípony súboru, ktorú používateľ vyberie pri ukladaní.

### Save As PNG

Pri exporte do PNG:

- skopíruje sa existujúci preview PNG

### Save As PSD

Pri exporte do PSD:

- ak už existuje pracovné PSD, použije sa ono
- ak bol zdroj RAW, pripraví sa PSD cez `RawToPsd.exe`
- ak bol zdroj PNG, PSD sa vytvorí cez `Magick.NET`

### Save As RAW

Pri exporte do RAW:

1. najprv sa pripraví PSD ako pracovný formát
2. toto PSD sa skopíruje do dočasného priečinka
3. nad ním sa spustí `PsdToRaw.exe`
4. vzniknutý RAW sa skopíruje na požadované cieľové miesto

Dôležité:

- pri RAW exporte sa naozaj spúšťa konverzná pipeline

## Save to Project flow

`Save to Project...` používa rovnaký exportný základ, ale výsledok ukladá do projektovej štruktúry `/Bits`.

Aktuálny flow je:

1. pripraviť PSD, ak ešte neexistuje
2. spustiť `PsdToRaw.exe`
3. získať výsledný `.raw`
4. uložiť ho do zvoleného miesta v projekte



## Čo som overil tooloch

Potvrdili sme tieto fakty:

- `RawToPsd.exe` sa spúšťa s jedným vstupným argumentom
- `PsdToRaw.exe` sa spúšťa s jedným vstupným argumentom
- oba tooly pracujú s rovnakým basename výstupu ako má vstupný súbor

Príklady:

- `RawToPsd.exe "foo.raw"` -> očakávaný výstup `foo.psd`
- `PsdToRaw.exe "foo.psd"` -> očakávaný výstup `foo.raw`



## Hlavné súbory



- [src/ViewModels/TextureLabViewModel.cs](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/ViewModels/TextureLabViewModel.cs)
- [src/Views/TextureLabView.axaml](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Views/TextureLabView.axaml)
- [src/Services/RawTextureConverter.cs](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)
- [src/Services/ExternalTextureToolService.cs](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/ExternalTextureToolService.cs)
- [src/Services/BundledToolPaths.cs](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/BundledToolPaths.cs)
- [src/Models/TextureFormat.cs](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Models/TextureFormat.cs)
- [src/Views/SettingsView.axaml](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Views/SettingsView.axaml)
- [src/DungeonSiegeLab.csproj](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/DungeonSiegeLab.csproj)

## Aké design patterns

### Facade

Najvýraznejší pattern v tejto implementácii je **Facade**.

Trieda `RawTextureConverter` funguje ako facade nad celou konverznou logikou:

- rozpoznanie formátu
- príprava temp súborov
- RAW preview konverzia
- PSD/PNG konverzia
- `Save As...`
- `Save to Project...`

Vďaka tomu UI vrstva nemusí vedieť, ako konverzia prebieha interne.

### Strategy

urobit singketon z tried
presunut kod do factory public RawTextureConverter()

poznamka pod čiarou big switch to strategy

Pri `Save As...` teraz používame  **čistý Strategy pattern**.

Export do jednotlivých formátov je rozdelený do samostatných stratégií:

- `PngTextureExportStrategy`
- `PsdTextureExportStrategy`
- `RawTextureExportStrategy`

Každá stratégia implementuje spoločné rozhranie `ITextureExportStrategy` a pozná len svoju vlastnú exportnú logiku.

Výber stratégie zabezpečuje `TextureExportStrategyFactory`, ktorá podľa cieľového formátu vyberie správnu implementáciu.

Vďaka tomu:

- `RawTextureConverter` už neobsahuje veľký `switch` pre `Save As...`
- exportná logika je oddelená podľa formátu
- architektúra je čistejšia a ľahšie rozšíriteľná

### Factory

Používame aj **Factory** myšlienku pri výbere conversion flow podľa formátu súboru.

Pomáha tomu:

- `TextureFormat`
- rozpoznanie prípony
- rozhodnutie, aká pipeline sa má použiť pre vstup alebo výstup

Krátke objasnenie:

`Factory` používame preto, aby `RawTextureConverter` nemusel priamo poznať konkrétne exportné triedy a rozhodovať cez vlastný `switch`, ktorú z nich má použiť. Namiesto toho zavolá `TextureExportStrategyFactory`, ktorá podľa cieľového formátu vráti správnu exportnú stratégiu. Tým je výber objektu oddelený od samotného používania objektu a kód je čistejší a ľahšie rozšíriteľný.

### Command

Na úrovni UI používame **Command** pattern cez MVVM commandy:

- open texture
- save as
- import replacement
- close tab
- go back

To drží používateľské akcie oddelene od view kódu.

### Bezpečný temp working flow

Nie je to klasický GoF pattern, ale je to dôležité architektonické rozhodnutie:

- pracujeme cez temp kópie
- nekonvertujeme priamo nad originálnymi assetmi
- pomocné súbory nevznikajú vedľa zdrojových súborov

Tento prístup znižuje riziko:

- poškodenia originálnych dát
- nechceného zapisovania do projektu
- bordelu v asset priečinkoch

## Pattern summary pre Save As

Funkcia `Save As...` používa hlavne tieto patterny:

- **Facade**: `RawTextureConverter` skrýva zložitosť konverzie
- **Strategy**: exportný algoritmus je rozdelený do samostatných strategy tried podľa cieľového formátu
- **Factory**: `TextureExportStrategyFactory` vyberá správnu exportnú stratégiu
- **Command**: UI spúšťa akcie cez MVVM commandy


(C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs), ale v tom, že dnes má runtime závislosť dodanú cez konštruktor.

Konkrétne miesta:

- [`src/Services/RawTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs)
  Trieda drží `private readonly Func<string, string, Task> _exportRawAsync;`
  To znamená, že nevie fungovať sama od seba, ale potrebuje pri vytvorení dostať konkrétnu implementáciu exportu.

- [`src/Services/RawTextureExportStrategy.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureExportStrategy.cs)
  Konštruktor `public RawTextureExportStrategy(Func<string, string, Task> exportRawAsync)` bráni jednoduchému `Instance = new();`, lebo bez parametra ju nevytvoríš.

- [`src/Services/RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)
  V konštruktore sa skladá factory takto:
  `new RawTextureExportStrategy(ExportRawAsync)`
  Čiže stratégia je priamo naviazaná na inštančnú metódu konkrétneho `RawTextureConverter`.

- [`src/Services/RawTextureConverter.cs`](C:/Users/patri/Documents/Dungeon%20Siege%20LOA/Bits/DungeonSiegeLab/src/Services/RawTextureConverter.cs)
  Metóda `ExportRawAsync(...)` nie je statická a používa `_toolService`, teda stav konkrétnej inštancie convertera. Preto ju dnes len tak nevytiahneš do globálneho singletonu bez zmeny väzby.

Stručne:
`Png` a `Psd` stratégie sú bezstavové a samostatné, preto singleton idú ľahko.  
`RawTextureExportStrategy` je naviazaná na konkrétny `RawTextureConverter` cez delegáta `ExportRawAsync`, a práve to je dôvod, prečo nejde rovnako jednoducho spraviť ako singleton.


skusiť to dostat ako ergument metody