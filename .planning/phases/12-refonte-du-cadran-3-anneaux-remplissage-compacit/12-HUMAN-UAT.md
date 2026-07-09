# Phase 12 — UAT humain (validation sur écran réel)

**Statut : EN ATTENTE de validation utilisateur**
Généré le 2026-07-09 à l'exécution du plan 12-02 (mode autonome : tout ce qui est automatisable a été
vérifié programmatiquement ; les critères ci-dessous sont **purement visuels** — remplissage, lisibilité
à 170 px, exactitude de l'anneau 24 h, présence/format du % — et exigent un écran réel : ils ne peuvent
PAS être automatisés).

## Contexte

Le plan 12-02 recompose `MainWindow.xaml` pour livrer visuellement la refonte v1.3 : remplissage inversé
(les arcs se REMPLISSENT vers le reset via `FractionElapsed`), 3 anneaux réordonnés du centre vers
l'extérieur (hebdo R44 → 5 h R58 → 24 h R72), nouvel anneau timeline 24 h coloré et gradué aux resets 5 h,
% d'utilization à côté de chaque countdown, et resize de l'overlay à 170 px. Aucune logique nouvelle :
uniquement du binding sur les propriétés/contrôles livrés par le plan 12-01. Les vérifications
programmatiques suivantes ont déjà été confirmées automatiquement (checkpoint auto-vérifié) :

- `dotnet build Chronos.sln -c Debug` : **0 avertissement, 0 erreur** (XAML inclus).
- `dotnet test Chronos.sln -c Debug` : **209/209 tests verts** (aucune régression, aucun test XAML cassé).
- Structure XAML vérifiée par grep :
  - Fenêtre `Width="170"` / `Height="170"` (TAILLE-01) ; ellipse de fond 156, centre 85,85.
  - Ordre des `RingArc` valeur : `ArcHebdo` (Radius 44) → `ArcCinqHeures` (Radius 58) →
    `ArcVingtQuatreHeures` (Radius 72) (VIS-02).
  - `ArcHebdo` / `ArcCinqHeures` bindent `FractionElapsed` (remplissage inversé VIS-01) ;
    `ArcVingtQuatreHeures` binde `DayFraction` (JOUR-01) + `Stroke = FiveHour.Utilization` via `UtilBrush`
    (JOUR-03).
  - `TickRing Angles="{Binding DayResetAngles}"` présent (JOUR-02) ; token `Piste24h` ajouté.
  - `FiveHour.UtilizationText` / `SevenDay.UtilizationText` affichés, séparateur « · » lié à
    `HasUtilizationText` (VIS-05, absent si utilization null).
- Republication exe self-contained mono-fichier win-x64 (~76,8 Mo) : **build de publication réussi**
  (instance précédente arrêtée avant publish pour libérer le verrou). Exe NON lancé par l'exécuteur —
  lancement/capture délégués à l'orchestrateur.

## Comment lancer

```
# exe republié :
src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe
# ou en dev :
dotnet run --project src/Chronos/Chronos.csproj -c Debug
```

## Critères à valider manuellement (écran réel requis)

| # | Exigence | Étape de vérification | Résultat attendu | Statut |
|---|----------|-----------------------|------------------|--------|
| 1 | TAILLE-01 | Lancer l'overlay | La fenêtre apparaît à ~170 px, sans crash | ⬜ |
| 2 | VIS-01 | Observer les arcs 5 h et hebdo au fil du temps | Les arcs sont PLUS remplis quand il reste PEU de temps avant le reset (vides en début de fenêtre, pleins au reset) | ⬜ |
| 3 | VIS-02 | Observer l'empilement des anneaux | Du centre vers l'extérieur : hebdo, puis 5 h, puis 24 h — sans chevauchement des 3 anneaux ni du texte | ⬜ |
| 4 | JOUR-01 | Observer l'anneau 24 h (externe) | Rempli de minuit à maintenant (à 18 h ≈ 3/4 du tour) | ⬜ |
| 5 | JOUR-02 | Observer les ticks sur l'anneau 24 h | Un tick à chaque reset 5 h projeté, cohérent avec l'heure réelle | ⬜ |
| 6 | JOUR-03 | Comparer la couleur de l'anneau 24 h et de l'anneau 5 h | Même couleur (rampe d'utilization 5 h) | ⬜ |
| 7 | VIS-05 | Lire le bloc central | « countdown · % » lisible pour chaque fenêtre ; « ~ » si source estimée ; AUCUN % ni séparateur si utilization absente (badge « estimée » cohérent) | ⬜ |
| 8 | TAILLE-01 | Vérifier la compacité globale | 3 anneaux + 2 lignes de texte lisibles, aucun chevauchement à 170 px | ⬜ |
| 9 | TAILLE-01 | Clic gauche pour déplacer (drag) puis relâcher près d'un coin (snap) | Le drag fonctionne et le snap place correctement l'overlay à 170 px | ⬜ |

## Consigne en cas d'écart

Tout réglage visuel qui gêne (police trop large, gap d'anneau, longueur de tick, chevauchement) est
consigné comme **ajustement** : modifier les valeurs concrètes du plan (rayons/épaisseurs/FontSize dans
`MainWindow.xaml`) puis re-vérifier. Ne pas « corriger en douce » une anomalie fonctionnelle sans
replanifier (`--gaps`).
