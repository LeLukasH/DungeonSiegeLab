# DungeonSiegeLab

Desktopová aplikácia na správu Dungeon Siege modding súborov v priečinku `/Bits`.  
Postavená na **.NET 8 + Avalonia UI**.

---

## Požiadavky

- [.NET SDK](https://aka.ms/dotnet/download)
- (Voliteľné) `RawToPsd.exe` nástroj pre konverziu `.raw` → `.psd` textúr

---

## Spustenie

```bash
cd src
dotnet run
```

alebo otvor `DungeonSiegeLab.sln` v **Visual Studio 2022** / **JetBrains Rider**.

---

## Funkcie

### 📁 Project Browser
| Funkcia | Popis |
|---|---|
| Načítanie /Bits | Otvorí priečinok a zobrazí stromovú štruktúru |
| Template browser | Klik na template → zobrazenie zdrojového kódu |
| Identify Textures | Nájde všetky textúry a otvorí ich v Texture Lab |
| Identify Dependencies | Nájde `specializes` závislosti template |

### 🎨 Texture Lab
| Funkcia | Popis |
|---|---|
| Náhľad textúry | Zobrazí `.raw` / `.psd` ako PNG |
| Info panel | Rozmery, Status (NOT OK ak nie sú deliteľné 16), použitie |
| Save to project | Uloží textúru späť do `/Bits` (PNG → RAW + .gas) |
| Save to disk | Exportuje ako PNG |
| Import replacement | Nahradí textúru tvojou editovanou verziou |

---

## Architektúra & Design Patterny

```
src/
├── Models/              # Dátové modely
│   ├── BitsNode.cs      # ⭐ COMPOSITE – strom priečinkov/súborov/templates
│   ├── TextureReference.cs
│   └── LoadedTexture.cs
│
├── Patterns/            # GoF design patterny
│   ├── ITextureProcessor.cs      # ⭐ STRATEGY – interface
│   ├── RawTextureProcessor.cs    # ⭐ STRATEGY – .raw konverzia
│   ├── PsdTextureProcessor.cs    # ⭐ STRATEGY – .psd konverzia
│   └── TextureProcessorFactory.cs # ⭐ FACTORY – výber stratégie
│
├── Services/
│   ├── GasParser.cs          # Parsuje .gas súbory
│   ├── TextureFinder.cs      # Regex hľadanie textúr
│   ├── BitsLoader.cs         # Načíta /Bits do stromu
│   └── RawTextureConverter.cs # ⭐ FACADE – obaľuje konverziu
│
├── ViewModels/          # MVVM (CommunityToolkit.Mvvm)
│   ├── MainViewModel.cs
│   ├── ProjectBrowserViewModel.cs
│   ├── TextureLabViewModel.cs
│   ├── TextureTabViewModel.cs
│   ├── SaveToProjectViewModel.cs
│   └── BitsNodeViewModel.cs
│
└── Views/               # Avalonia AXAML UI
    ├── MainWindow.axaml
    ├── ProjectBrowserView.axaml
    ├── TextureLabView.axaml
    └── SaveToProjectDialog.axaml
```

### Design patterny v kóde

| Pattern | Trieda | Účel |
|---|---|---|
| **Composite** | `BitsNode` / `BitsFolder` / `BitsFile` / `BitsTemplate` | Stromová štruktúra /Bits |
| **Strategy** | `ITextureProcessor` + implementácie | `.raw` vs `.psd` konverzia |
| **Factory** | `TextureProcessorFactory` | Výber správnej Strategy |
| **Facade** | `RawTextureConverter` | Skrytie komplexnosti konverzie |
| **Observer** | `INotifyPropertyChanged` (MVVM) | UI reakcia na zmeny dát |
| **Command** | `IRelayCommand` (CommunityToolkit) | Tlačidlové akcie |

---

## Závislosti (NuGet)

| Balíček | Verzia | Účel |
|---|---|---|
| Avalonia | 11.1.0 | Cross-platform UI |
| Avalonia.Themes.Fluent | 11.1.0 | Dark theme |
| CommunityToolkit.Mvvm | 8.3.0 | MVVM boilerplate |
| Magick.NET-Q8-AnyCPU | 13.9.0 | PSD/RAW → PNG konverzia |
