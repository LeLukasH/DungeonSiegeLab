# Prípad použitia: Identifikácia závislostí

## Cieľ
Popísať postup, kedy používateľ načíta rep, vyberie súbor a šablónu, a spustí identifikáciu závislostí.

## Hlavný aktér
- Používateľ

## Pomocné komponenty
- ProjectBrowserViewModel (príkazy + stav UI)
- DependencyFinder (analýzny engine)
- TextureFinder (rozlišovanie ciest textúr)

## Prekondície
- Aplikácia je spustená.

## Hlavný tok

1. **Používateľ** otvorí aplikáciu.
2. **Systém** automaticky načíta Untank dáta a zobrazí strom.

3. **Používateľ** vyberie priečinok Bits.
4. **Systém** načíta súbory a zobrazí projekt tree.

5. **Používateľ** rozbalí adresár a vyberie .gas súbor.
6. **Systém** zobrazí šablóny vnútri súboru.

7. **Používateľ** vyberie šablónu.
8. **Systém** otvorí kartu so zdrojovým kódom šablóny.

9. **Používateľ** klikne na Identify.
10. **Systém** parsuje šablónu, aplikuje pravidlá interpretátora.
11. **Systém** rekurzívne prechádza specializes, filtruje prepísané závislosti, deduplikuje.
12. **Systém** vyrieši textúrové závislosti v Bits + Untank.
13. **Systém** zobrazí panel závislostí s výsledkami a stavovým súhrnom.

## Alternatívne toky
- Používateľ klikne Identify bez vybranej šablóny:
  Systém zobrazí chybu „Najprv vyberte šablónu.“

- Šablóna nemá žiadne závislosti:
  Systém zobrazí správu, že závislosti neboli nájdené.

- Súbor dependency-rules.json chýba alebo je neplatný:
  Systém použije predvolené pravidlá a pokračuje.

- Cyklus v reťazci specializes:
  Systém bezpečne ukončí rekurziu.

## Výsledok
- Používateľ vidí kompletný panel závislostí pre vybranú šablónu, vrátane dedených položiek a metadát zdrojovej cesty.

## Diagram

### Sekvenčný diagram
- PlantUML: diagrams/dependency-identification-sequence.puml
