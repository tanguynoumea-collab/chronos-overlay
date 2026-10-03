---
phase: 42-un-geste-sur-toute-la-silhouette
plan: 03
subsystem: views/gestes
tags: [tdd, gst-01, gst-02, gst-03, repartiteur, hit-test, drag-move, fenetre-layered]
requires:
  - "42-01 (AutomateGeste)"
  - "42-02 (ZoneGeste, token ZoneSilhouette, silhouettes des cinq vues)"
provides:
  - "Répartiteur unique sur la grille Racine de MainWindow (Racine_MouseLeftButtonDown / MouseMove / MouseLeftButtonUp / LostMouseCapture)"
  - "Couture interne MainWindow.LirePosition (tests de câblage)"
  - "Pastilles de MainWindow en ZoneSilhouette ; CentreHit et DragMove de fenêtre supprimés"
affects:
  - "42-04 (documentation des gestes : réglages, README, note de version)"
tech-stack:
  added: []
  patterns:
    - "Filtre géométrique (ZoneGeste.Contient) avant d'armer un geste sur une racine sans fond"
    - "Garde runtime des pinceaux : silhouettes, ButtonBase et gabarit, porteurs d'infobulle tous d'alpha ≥ 1"
    - "Clic droit levé par MouseUp (bouton droit) bouillonnant, MouseRightButtonUp étant un événement direct"
key-files:
  created: []
  modified:
    - src/Chronos/Views/MainWindow.xaml
    - src/Chronos/Views/MainWindow.xaml.cs
    - tests/Chronos.Tests/GardeGestesCadranTests.cs
    - tests/Chronos.Tests/ZonesGesteRenduTests.cs
    - .planning/phases/42-un-geste-sur-toute-la-silhouette/42-VALIDATION.md
decisions:
  - "Couture interne LirePosition (Func<MouseEventArgs, IInputElement, Point>) : GetPosition lit le vrai périphérique, impossible à placer en test ; le câblage réel est prouvé sans lancer l'overlay"
  - "Commentaires de MainWindow.xaml : « ZoneSilhouette (alpha 1) » et non « (#01000000) », la garde interdit l'alpha littéral dans le fichier (même choix qu'au plan 02)"
  - "Clic droit testé en levant UIElement.MouseUpEvent (bouton droit) : MouseRightButtonUpEvent est DIRECT et ne remonterait pas jusqu'à Racine"
  - "Pastilles inertes (âge, hors ligne) hors du disque d'Arcs / Braises : captent la souris sans geste, voulu ; consigné au constat manuel"
metrics:
  duration: "~25 min"
  completed: 2026-10-03
  tasks: 2
  files: 5
---

# Phase 42 Plan 03 : un seul geste sur toute la silhouette — Summary

Le disque `CentreHit` de 66 px et le `DragMove` déclenché par la fenêtre ont été supprimés. Ils sont remplacés par UN
répartiteur posé sur la grille `Racine` de `MainWindow`. L'appui n'est pris que s'il tombe dans la silhouette visible
(`ZoneGeste.Contient`). `AutomateGeste` décide ensuite :
- double-clic : `ClicCentre(2)` immédiat ;
- clic relâché sans bouger : `ClicCentre(1)`, la bascule a lieu à l'échéance de l'arbitre ;
- dépassement du seuil Windows : `DragMove()` sous la garde `_enDeplacement … finally`, puis `SnapToNearestCorner()`.

Le clic droit ouvre les réglages partout où il arrive. Les pastilles boutons et la pastille d'âge sont peintes en
`ZoneSilhouette`. Le seul `"Transparent"` restant est celui de la `<Window>`.

## Tâches

| # | Nom | Commit | Fichiers |
|---|-----|--------|----------|
| 1 | RED : garde textuelle réécrite, routage HitTest, pastilles, clic droit, garde de pinceaux | 090b614 | GardeGestesCadranTests.cs, ZonesGesteRenduTests.cs |
| 2 | GREEN : répartiteur unique, CentreHit supprimé, pastilles en ZoneSilhouette, commentaires corrigés | fcb8819 | MainWindow.xaml, MainWindow.xaml.cs |
| + | Test de câblage réel du répartiteur (demandé par le vérificateur) et note du constat manuel | aeec890 | ZonesGesteRenduTests.cs, 42-VALIDATION.md |

## Vérification

- RED : 9 échecs par assertion (6 dans la garde textuelle ; nom `Racine`, pastille d'âge, garde de pinceaux), et le projet compile. Trois tests du plan 03 étaient déjà verts au RED : HitTest, pastille bouton, clic droit. Les silhouettes du plan 02 suffisaient déjà à router ces cas.
- Filtre de la tâche 2 (`GardeGestesCadran|ZonesGesteRendu|AutomateGeste|ArbitreClicCentre|CadranBinding|OverlayController|MainViewModelTests|ReglagesWindowTests`) : 162/162.
- Suite complète : 1921/1921. `dotnet build Chronos.sln` : 0 avertissement en Debug comme en Release.
- Test de câblage `Le_repartiteur_reel_arme_la_bascule_dans_la_silhouette_et_rien_dehors`. Il monte un `MainWindow` et un `MainViewModel` réels, avec l'arbitre en pas à pas par horloge injectée :
  - au point (85,40), l'appui est pris (Handled) et la bascule n'a pas encore eu lieu ; elle est effective à l'échéance ;
  - au point (2,2), l'appui n'est pas pris et rien n'est armé ;
  - un relâchement seul n'a aucun effet.
- Deux mutations manuelles, non commitées, font rougir ce test : la suppression de `ClicCentre(1)` au relâchement, et la suppression du filtre `ZoneGeste.Contient`.
- Comptes d'acceptation : `CentreHit` 0/0 ; `x:Name="Racine"` 1 ; `MouseLeftButtonDown=` 1 ; `"Transparent"` 1 ; `ZoneSilhouette` 8 ; `MinimumHorizontalDragDistance` 1 ; `AddHandler` 0.
- Phase 40 préservée : la garde `_enDeplacement = true … finally` est autour de `DragMove`, et `SizeChanged` / `DpiChanged` sont intacts (`Le_changement_d_empreinte_recale_sur_le_coin_courant` reste vert).

## Écarts au plan

### Corrections automatiques

**1. [Rule 3 - Blocage] Couture `LirePosition` ajoutée à `MainWindow`**
- **Trouvé pendant :** le test de câblage demandé par le vérificateur.
- **Problème :** `MouseButtonEventArgs.GetPosition` lit le vrai périphérique souris. Un test ne peut pas placer le pointeur en (85,40) sans lancer l'overlay, ce qui est interdit.
- **Correction :** ajout de la propriété `internal Func<MouseEventArgs, IInputElement, Point> LirePosition`, qui vaut `e.GetPosition(relatif)` par défaut. Les gestionnaires passent par elle, et la garde textuelle (`ZoneGeste.Contient(`, `e.ClickCount`, etc.) reste satisfaite.
- **Commit :** fcb8819 (code), aeec890 (test).

**2. [Rule 1 - Bug de test] Clic droit levé par `MouseUpEvent` et non par `MouseRightButtonUpEvent`**
- **Problème :** `MouseRightButtonUpEvent` est un événement DIRECT. Levé sur l'élément touché, il n'atteindrait jamais `OnRightClick`, qui est porté par `Racine`.
- **Correction :** le test lève `UIElement.MouseUpEvent` avec le bouton droit. WPF le relève en `MouseRightButtonUp` sur chaque élément du chemin, comme le fait le périphérique réel. `MouseRightButtonUpEvent` reste cité dans le commentaire du test.
- **Commit :** 090b614.

**3. Ajustements de texte imposés par les gardes**
- Les commentaires écrivent « ZoneSilhouette (alpha 1) » : la garde interdit `#01000000` dans `MainWindow.xaml`.
- « Pas d'AddHandler(…, true) » a été reformulé en « pas d'abonnement « handled inclus » », car la garde interdit `AddHandler(` dans le code-behind.
- Le commentaire ACC-02 du constructeur dit maintenant « le clic sur le cadran » et non plus « le clic au centre », chaîne interdite par la garde.

### Ajouts demandés par le vérificateur de plans
- Test `[WpfFact]` de câblage réel du répartiteur (voir Vérification).
- **Pastilles inertes :** les pastilles d'âge et hors ligne sont peintes à alpha ≥ 1 hors du disque d'Arcs et de Braises. Elles captent donc la souris : un clic gauche n'y produit aucun geste et ne traverse pas vers le bureau, et le clic droit y ouvre les réglages. **C'est voulu, ce n'est pas une régression.** Ce point est consigné dans `42-VALIDATION.md`, section Manual-Only.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/MainWindow.xaml, src/Chronos/Views/MainWindow.xaml.cs, tests/Chronos.Tests/GardeGestesCadranTests.cs, tests/Chronos.Tests/ZonesGesteRenduTests.cs
- FOUND : 090b614, fcb8819, aeec890
