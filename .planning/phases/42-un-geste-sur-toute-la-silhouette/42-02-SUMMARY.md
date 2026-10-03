---
phase: 42-un-geste-sur-toute-la-silhouette
plan: 02
subsystem: views/cadrans
tags: [tdd, gst-02, gst-03, hit-test, fenetre-layered, render-target-bitmap]
requires:
  - "42-01 (AutomateGeste) — non consommé ici, utilisé au plan 03"
provides:
  - "Token ZoneSilhouette (#01000000) dans Resources/DesignTokens.xaml"
  - "Views/ZoneGeste.cs : propriété attachée Silhouette + TrouverToutes / Trouver / Contient (répartiteur du plan 42-03)"
  - "Une silhouette par variante : disque 148 centré (Arcs, Braises), rectangle arrondi 10 par gabarit (Fusible, Marée, Volets)"
affects:
  - src/Chronos/Views/MainWindow.xaml(.cs) (plan 42-03 : retrait de CentreHit, routage par ZoneGeste)
tech-stack:
  added: []
  patterns:
    - "Preuve de zone cliquable par lecture de l'alpha (RenderTargetBitmap du Content mis en page à l'empreinte), pas par HitTest"
    - "Points témoins générés depuis le contrat géométrique, auto-contrôlés (marge 2 px, secteur de la flèche exclu)"
key-files:
  created:
    - src/Chronos/Views/ZoneGeste.cs
    - tests/Chronos.Tests/ZonesGesteRenduTests.cs
  modified:
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Views/Cadrans/CadranArcsView.xaml
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Views/Cadrans/CadranFusibleView.xaml
    - src/Chronos/Views/Cadrans/CadranMareeView.xaml
    - src/Chronos/Views/Cadrans/CadranVoletsView.xaml
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
    - tests/Chronos.Tests/CadransOrientationBindingTests.cs
decisions:
  - "Silhouette posée en PREMIER enfant (sous tout le contenu), DynamicResource ZoneSilhouette ; #01000000 n'est écrit que dans DesignTokens.xaml (les commentaires des vues disent « alpha 1 » pour rester compatibles avec les gardes textuelles)"
  - "ZoneGeste.TrouverToutes saute les sous-arbres Visibility != Visible (jamais IsVisible, faux hors écran) ; parcours de l'arbre visuel et non FindName (portées de noms des UserControl)"
  - "Test 4 en [Fact] textuel (lecture de fichiers, pas de WPF)"
metrics:
  duration: "~12 min"
  completed: 2026-10-03
  tasks: 2
  files: 10
---

# Phase 42 Plan 02 : silhouette de geste déclarée par chaque cadran — Summary

Chaque vue de cadran pose désormais sous son contenu une forme `ZoneSilhouette` (alpha 1, invisible), marquée
`vues:ZoneGeste.Silhouette="True"` : un disque de 148 centré pour Arcs et Braises, un rectangle arrondi de rayon 10
à l'empreinte pour chaque gabarit de Fusible, Marée et Volets. Un rendu `RenderTargetBitmap` prouve, pour les huit
variantes et les deux états (nominal, indisponible), un alpha > 0 en chaque point témoin dedans (entre-anneaux
compris) et un alpha = 0 en chaque point témoin dehors. Le routage (`CentreHit`, `DragMove`) n'a pas changé : il est
prévu au plan 03.

## Tâches

| # | Nom | Commit | Fichiers |
|---|-----|--------|----------|
| 1 | RED : ZonesGesteRenduTests (4 tests, 8 variantes) | eae0264 | tests/Chronos.Tests/ZonesGesteRenduTests.cs |
| 2 | GREEN : token, ZoneGeste, silhouettes des cinq vues, deux tests ajustés | 98c34f4 | DesignTokens.xaml, ZoneGeste.cs, 5 vues Cadran*View.xaml, CadransThemeBindingTests.cs, CadransOrientationBindingTests.cs |

## Vérification

- Filtre `ZonesGesteRenduTests|GardeCouleursCadrans|CadransThemeBinding|CadransOrientationBinding|CadranBindingTests|EmpreinteCadran` : 73/73 verts.
- Suite complète : 1910/1910 verts. `dotnet build Chronos.sln` : 0 avertissement.
- Mutation manuelle (non commitée) : avec `ZoneSilhouette` passé à `#00000000`, `Le_rendu_capte_dans_la_silhouette_et_laisse_passer_dehors` rougit sur tous les témoins dedans hors contenu. Le test détecte donc bien la régression.
- Phase 41 intacte : `git show HEAD -- CadranBraisesView.xaml | grep -cE "^-.*(FlecheReset|Polygon|GroupSize|HeureResetBraises)"` = 0.
- Comptes : `ZoneGeste.Silhouette="True"` 1 (Arcs, Braises) et 2 (Fusible, Marée, Volets). `Background="Transparent"` et `#01000000` : 0 dans les vues.
- `CentreHit` et `Cadran_MouseLeftButtonDown` sont toujours présents, et `GardeGestesCadranTests` reste vert.

## Écarts au plan

Aucun écart fonctionnel. Précisions d'exécution :
- Les commentaires ajoutés dans les vues écrivent « ZoneSilhouette (alpha 1) » au lieu de « (#01000000) ». La raison : le test 4 interdit la chaîne `#01000000` dans les vues, et `GardeCouleursCadransTests.InterditXaml` refuse tout hexadécimal, commentaires compris.
- Ajout de `using Path = System.IO.Path;` dans le fichier de tests, pour lever l'ambiguïté avec `System.Windows.Shapes.Path`.
- En plus du contrat, le test 1 vérifie `ZoneGeste.Contient` : centre dedans pour le disque, coin (0,5 ; 0,5) dehors pour le rectangle. Il vérifie aussi `Trouver == TrouverToutes[0]`.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/ZoneGeste.cs
- FOUND : tests/Chronos.Tests/ZonesGesteRenduTests.cs
- FOUND : eae0264, 98c34f4
