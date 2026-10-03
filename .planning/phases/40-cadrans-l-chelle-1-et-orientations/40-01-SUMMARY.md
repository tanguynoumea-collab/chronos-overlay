---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 01
subsystem: rendering / settings
tags: [cadran, tokens, empreinte, orientation, tdd]
requires: []
provides:
  - "EmpreinteCadran.Pour(CadranStyle, OrientationCadran) : Size (fonction pure, Rendering)"
  - "20 sys:Double Cadran* dans DesignTokens.xaml (16 empreintes + 4 corps)"
  - "enum neutre OrientationCadran + ChronosSettings.OrientationFusible/Maree/Volets (défauts H/V/H)"
  - "EmpreinteCadranTests.TaillesCadran (table contractuelle publique)"
affects: [40-02, 40-03, 40-04, 40-05, 40-06, 40-07]
tech-stack:
  added: []
  patterns: ["table contractuelle de tokens prouvée par XDocument", "miroir C# pur d'un dictionnaire XAML"]
key-files:
  created:
    - src/Chronos/Rendering/EmpreinteCadran.cs
    - tests/Chronos.Tests/EmpreinteCadranTests.cs
  modified:
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Services/ChronosSettings.cs
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
decisions:
  - "Les empreintes vivent en double : tokens XAML (vérité visuelle) + EmpreinteCadran (miroir C# sans STA), égalité prouvée par test"
  - "OrientationCadran est un enum neutre dans Services (pas l'Orientation de WPF) ; initialiseurs explicites par propriété (Marée = Vertical)"
  - "SettingsService inchangé : la lecture tolérante de la phase 36 couvre les nouveaux enums"
metrics:
  duration: "~10 min"
  completed: 2026-10-03
  tasks: 2
  files: 6
---

# Phase 40 Plan 01 : empreintes de cadran et orientation persistée — Summary

Contrat pur des cadrans à l'échelle 1 : 20 tokens `Cadran*` dans `DesignTokens.xaml`, la fonction pure
`EmpreinteCadran.Pour(style, orientation)` prouvée égale aux tokens par lecture XDocument, et l'enum neutre `OrientationCadran`
persisté par cadran dans `ChronosSettings` (Fusible H, Marée V, Volets H), avec retombée tolérante sur valeur fautive.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : table `TaillesCadran`, 10 empreintes, miroir des tokens, décompte global, 3 tests d'orientation | a2ac4bb |
| 2 | GREEN : 20 tokens, `EmpreinteCadran.cs`, enum + 3 propriétés | 69363c4 |

## Vérification

- RED : le build du projet de test échouait sur `OrientationCadran` / `EmpreinteCadran` inexistants (24 erreurs CS0103/CS0246).
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1777/1777 tests réussis (dont `Chaque_enum_des_reglages_retombe_sur_son_propre_defaut`, qui couvre les 3 nouveaux enums, et `ServicesLayerPurity`).
- Critères grep : 20 `<sys:Double x:Key="Cadran`, signature `Pour` présente, enum présent, `OrientationMaree ... = OrientationCadran.Vertical`, 0 « System.Windows » dans ChronosSettings.cs.

## Écarts par rapport au plan

**1. [Règle 1 - Cohérence avec les critères] Commentaire de l'enum reformulé**
- **Trouvé pendant :** tâche 2
- **Problème :** le `<summary>` dicté par le plan (« pas System.Windows.Controls.Orientation ») faisait échouer le critère `grep -c "System.Windows" ChronosSettings.cs == 0`.
- **Correctif :** reformulé en « pas l'Orientation des contrôles WPF ». Le sens et le code restent identiques.
- **Commit :** 69363c4

## Known Stubs

Aucun. Les tokens et `EmpreinteCadran` ne sont pas encore utilisés par les vues : c'est prévu, les plans 40-02 et suivants les branchent.

## Self-Check: PASSED

- FOUND : src/Chronos/Rendering/EmpreinteCadran.cs, tests/Chronos.Tests/EmpreinteCadranTests.cs
- FOUND : commits a2ac4bb, 69363c4
