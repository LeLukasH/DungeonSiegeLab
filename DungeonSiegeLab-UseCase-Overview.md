# DungeonSiegeLab — Prehľad prípadov použitia

## Účel
Dokument popisuje hlavné prípad použitia aplikácie DungeonSiegeLab a ich vzťahy.

## Hlavný aktér
- Používateľ (analytik, modifikátor, vývojár)

## Prípad 1: Prehliadanie repozitára

### UC1: Načítať Bits priečinok
Používateľ vyberie priečinok obsahujúci GAS súbory z Dungeon Siege projektu. Systém načíta všetky súbory a strukturu.

**Prekondícia:** Aplikácia je spustená.
**Výsledok:** Projekt tree je dostupný v UI.

### UC2: Prechádzať strom súborov
Používateľ rozbaľuje priečinky a prezerá si dostupné súbory a šablóny.

**Prekondícia:** Bits priečinok je načítaný (UC1).
**Výsledok:** Používateľ vidí kompletný strom súborov a šablón.

### UC3: Otvoriť šablónu
Používateľ vyberie konkrétnu šablónu a systém ju otvorí v karte s jej zdrojovým kódom.

**Prekondícia:** Strom súborov je dostupný (UC2).
**Výsledok:** Karta s šablónou je otvorená, kód je viditeľný.

---

## Prípad 2: Identifikácia závislostí

### UC4: Spustiť identifikáciu závislostí
Používateľ klikne na tlačidlo Identify. Systém analyzuje zdrojový kód šablóny a identifikuje všetky jej závislosti (textúry, zvuky, skripty, komponenty, atď.).

**Prekondícia:** Šablóna je vybraná (UC3).
**Výsledok:** Zoznam závislostí je vypočítaný.

### UC5: Zobraziť panel závislostí
Systém zobrazí všetky identifikované závislosti v paneli s detailmi ako typ, zdroj, riadok a pôvod (lokálna alebo dediečená).

**Prekondícia:** Identifikácia je spustená (UC4).
**Výsledok:** Panel je otvorený s výsledkami.

---

## Prípad 3: Texture Lab

### UC6: Zobraziť textúru (RAW / PSD / PNG)
Používateľ načíta textúru z identifikácie alebo manuálne vyberie súbor. Systém jej vyrieši náhľad (konverzia RAW → PSD → PNG) a zobrazí ju v editore.

**Prekondícia:** Textúra je vybraná (zo UC5 alebo manuálne).
**Výsledok:** Textúra a jej metadáta sú viditeľné.

### UC7: Exportovať textúru (PNG / PSD / RAW)
Používateľ vyberie cieľový formát a cestu. Systém konvertuje a exportuje textúru.

**Prekondícia:** Textúra je otvorená (UC6).
**Výsledok:** Súbor je uložený v zvolenom formáte.

### UC8: Importovať replacement
Používateľ vyberá náhradnú textúru (PNG, PSD, alebo RAW) a nahráva ju do otvorenú textúru v editore.

**Prekondícia:** Textúra je otvorená (UC6).
**Výsledok:** Textúra je zamení za náhradnú.

### UC9: Uložiť do projektu
Používateľ uloží textúru späť do projektu (priečinok Bits), vytvorí súbory .raw a .gas s patričným názvom a cestou.

**Prekondícia:** Textúra je otvorená (UC6).
**Výsledok:** Súbory sú uložené do projektu.

---

## Vzťahy medzi prípadmi

- **UC2 includes UC1:** Na prechádzanie stromu treba mať najprv načítaný projekt.
- **UC3 includes UC2:** Na otvorenie šablóny treba mať viditeľný strom.
- **UC4 includes UC3:** Identifikácia potrebuje vybranú šablónu.
- **UC4 includes UC5:** Po identifikácii sa zobrazí panel.
- **UC7 includes UC6:** Na export musí byť textúra otvorená.
- **UC8 includes UC6:** Na import musí byť textúra otvorená.
- **UC9 includes UC6:** Na uloženie musí byť textúra otvorená.

---

## Poznámky

- Textúra Lab pracuje s tromi formátmi: RAW (nativ Dungeon Siege), PSD (pracovný), PNG (náhľad).
- Identifikácia závislostí používa Interpreter pattern na analýzu pravidiel.
- Konverzie textúr vyžadujú externé nástroje (RawToPsd.exe, PsdToRaw.exe) a knižnicu Magick.NET.
