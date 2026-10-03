# Phase 36: Socle - Context

**Gathered:** 2026-10-03
**Status:** Ready for planning
**Source:** Cycle ZEUS n°2 — décisions verrouillées (pas de discuss-phase : tout est tranché en amont)

<domain>
## Phase Boundary

Rendre sûres les suppressions du milestone v1.9 avant qu'elles aient lieu : (1) lecture des réglages tolérante **valeur par
valeur** ; (2) garde d'arguments : tout `--xxx` inconnu sort en silence (code 0) **avant** le verrou mono-instance. Aucune
suppression de fonctionnalité dans cette phase (la purge est la phase 37, la suppression de `HistoriqueStyleSemaine` la 38).
Aucune UI.

</domain>

<decisions>
## Implementation Decisions

### SOC-01 — Réglages tolérants
- Constat (inventaire) : `SettingsService.Load()` (`src/Chronos/Services/SettingsService.cs:38-53`) attrape `JsonException`
  et renvoie `new ChronosSettings()` ENTIER ; `JsonStringEnumConverter` lève sur une valeur d'enum inconnue ⇒ un seul enum
  inconnu efface thème, coin, géométries, `InnerStatusLineCommand`, puis le prochain `Save(Load() with …)` réécrit ces
  défauts (perte définitive).
- Décision : une valeur d'énumération inconnue retombe sur **la valeur par défaut de CETTE propriété** ; toutes les autres
  propriétés sont conservées. Membres inconnus : ignorés (comportement System.Text.Json actuel, déjà testé
  `SettingsServiceTests.cs:208-224`). S'applique à tous les enums de `ChronosSettings` (`CadranStyle`, `CadranDisplayMode`,
  `SessionStyle`, `HistoriqueStyleSemaine` tant qu'il existe, `SectionReglages`, `OverlayCorner`, et tout enum futur comme
  l'orientation de la phase 40) — mécanisme générique (convertisseur tolérant ou équivalent), pas un cas par propriété.
- Un JSON réellement illisible (tronqué, non-JSON) reste toléré comme aujourd'hui (défauts, pas de crash) — mais ne doit pas
  produire de réglages « à moitié lus » ; le comportement retenu doit être explicite et testé.
- Le chargement tolérant doit être journalisé (une ligne de log/diagnostic quand une valeur est retombée sur son défaut), sans
  bruit à chaque lecture si possible.

### SOC-02 — Garde d'arguments
- Constat : `App.xaml.cs:23-31` aiguille `--statusline` (avant le verrou posé vers `:79-84`) ; `--hook`, `--cadrans`,
  `--historique`, `--sessions`, le mode CLI existent. Une invocation inconnue lancerait aujourd'hui l'overlay complet et
  heurterait le verrou (MessageBox « Chronos tourne déjà »).
- Décision : une fonction **pure** de tri des arguments (testable sans WPF) classe l'invocation : mode connu → comportement
  actuel ; argument commençant par `--` inconnu (ou retiré) → sortie silencieuse code 0, AVANT verrou, AVANT réconciliation
  des hooks, sans fenêtre. Aucun argument → overlay normal.
- Après la phase 37, `--statusline` deviendra un argument retiré : il devra tomber dans ce cas (la garde doit donc reposer sur
  une liste blanche des modes connus, pas sur une liste noire).
- Garde textuelle (test) sur l'ordre dans `App.xaml.cs` : le tri des arguments précède l'acquisition du verrou.

### Claude's Discretion
- Forme exacte du convertisseur tolérant (JsonConverterFactory générique vs pré-passe JsonNode) ; nom des types ; emplacement
  (`Services/`, sans type WPF — `ServicesLayerPurityTests`).
- Forme du journal de retombée.

</decisions>

<canonical_refs>
## Canonical References

- `.planning/REQUIREMENTS.md` — SOC-01, SOC-02
- `.planning/ROADMAP.md` — Phase 36 (critères de réussite)
- `.zeus/reports/cycle2/inventaire-themes-historique.md` § B3 « PIÈGE de lecture des réglages »
- `.zeus/reports/cycle2/inventaire-purge-donnees.md` § A6 (`--statusline` et verrou)
- `.zeus/reports/llm-council-2026-10-03.md` § 6 (garde d'arguments) et § 8 (P0 socle)
- `src/Chronos/Services/SettingsService.cs`, `src/Chronos/Services/ChronosSettings.cs`, `src/Chronos/App.xaml.cs`,
  `src/Chronos/Services/VerrouInstanceUnique.cs`
- `tests/Chronos.Tests/SettingsServiceTests.cs`, `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json`

</canonical_refs>

<specifics>
## Specific Ideas
- Fichier témoin de test : `{"ThemeKey":"nord","Corner":"BottomLeft","CadranStyle":"Spirale","HistoriqueStyleSemaine":"Tuiles","SessionStyle":"Inconnu","SectionReglages":"Fantome","InnerStatusLineCommand":"echo hi", …géométries}` → thème `nord`, coin `BottomLeft`, `InnerStatusLineCommand` et géométries conservés ; `CadranStyle=Arcs`, `SessionStyle=Pastilles`, etc.
- Le Save suivant réécrit le fichier sans perte.
</specifics>

<deferred>
## Deferred Ideas
None — la suppression effective de `HistoriqueStyleSemaine` est en phase 38, celle de `--statusline` en phase 37.
</deferred>

---
*Phase: 36-socle — Context gathered: 2026-10-03 (décisions verrouillées du cycle ZEUS n°2)*
