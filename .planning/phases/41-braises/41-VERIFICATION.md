---
phase: 41-braises
verified: 2026-10-03T00:00:00Z
status: passed
score: 3/3 must-haves verified
---

# Phase 41 : Braises — Rapport de vérification

**Objectif de la phase :** l'anneau 5 h de Braises compte 20 braises de 15 min en 5 groupes d'une heure séparés par un vide, avec une flèche fixe à midi (`TickReset`) et « ↻ HH:MM » (heure locale du reset 5 h, exacte, absente si inconnue ou dépassée) en mode temps uniquement.
**Vérifié le :** 2026-10-03
**Statut :** passed
**Re-vérification :** non, vérification initiale.

## Atteinte de l'objectif

### Vérités observables (critères de succès de la ROADMAP)

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | 20 braises en 5 groupes de 4, groupe sur 72°, pas 13,2°, rayon 4,0, vide entre groupes ; allumage selon `FractionRemaining` ; hebdo inchangé | ✓ VÉRIFIÉ | `CadranBraisesView.xaml` : anneau R66 `Count=20 PipRadius=4.0 GroupSize=4 GroupPitch=13.2`, `Fraction` lié à `FiveHour.FractionRemaining`. `BraisesGeometrie.Angle` (fonction pure) donne 16,2 / 29,4 / 42,6 / 55,8 puis +72° par groupe, soit un vide de 32,4° centré sur midi. L'anneau hebdo (R44, 12 braises) n'a ni `GroupSize` ni `GroupPitch` et garde la répartition uniforme, avec repli dans `Angle`. `EmberRingControl` appelle `BraisesGeometrie.Angle` pour le nominal et l'attente. |
| 2 | Flèche fixe à midi, hors anneau, triangle 10×7 + filet 12 px, `TickReset` du thème, sans animation | ✓ VÉRIFIÉ | `Canvas FlecheReset` 170×170 : `Polygon 80,5 90,5 85,12` (10×7) et `Line` de y=13 à y=25 (12 px), les deux en `{DynamicResource TickReset}`. `IsHitTestVisible=False`, aucune animation. `TickReset` suit `TextePrincipal` dans le thème (`ChronosTheme.cs:82`). |
| 3 | « ↻ HH:MM » (10,5, `TexteSecondaire`) en mode temps seulement, depuis `resets_at` ; absente si inconnu ; centre % inchangé | ✓ VÉRIFIÉ | `WindowGaugeViewModel.Interpolate` : `HasHeureReset = ResetsAt is {} && > now`, texte `"↻ " + HeureMinute(..., _fuseau)`, vide sinon. Un plancher garde l'heure, car elle vient du serveur. La vue place le `TextBlock` `HeureResetBraises` (FontSize 10.5, `TexteSecondaire`, visibilité sur `HasHeureReset`) uniquement dans le `StackPanel` `ShowCountdown`. Le panneau `ShowPercent` garde ses 2 lignes. |

**Score :** 3/3

### Artefacts requis

| Artefact | Rôle | Statut | Détails |
|----------|------|--------|---------|
| `src/Chronos/Rendering/BraisesGeometrie.cs` | angles purs avec groupes | ✓ VÉRIFIÉ | substantiel, branché (EmberRingControl) |
| `src/Chronos/Controls/Cadrans/EmberRingControl.cs` | propriétés `GroupSize` / `GroupPitch` | ✓ VÉRIFIÉ | rendu nominal et attente par les mêmes angles |
| `src/Chronos/Views/Cadrans/CadranBraisesView.xaml` | anneaux, flèche, 3e ligne | ✓ VÉRIFIÉ | voir vérités 1 à 3 |
| `src/Chronos/ViewModels/WindowGaugeViewModel.cs` | `HeureResetTexte`, `HasHeureReset` | ✓ VÉRIFIÉ | calculés à chaque `Interpolate` |
| `src/Chronos/ViewModels/CadranPreviewViewModel.cs` | échantillon de galerie | ✓ VÉRIFIÉ | heure seulement si le reset d'échantillon est futur, jamais sur l'hebdo |

### Liens clés

| De | Vers | Via | Statut |
|----|------|-----|--------|
| Vue Braises | `EmberRingControl` | `GroupSize` / `GroupPitch` | ✓ BRANCHÉ |
| `EmberRingControl` | `BraisesGeometrie.Angle` | appels aux lignes 84 et 102 | ✓ BRANCHÉ |
| Vue Braises | `FiveHour.HeureResetTexte` / `HasHeureReset` | bindings | ✓ BRANCHÉ |
| VM | `_state.ResetsAt` (serveur) | `Interpolate` | ✓ BRANCHÉ |

### Flux de données (niveau 4)

La 3e ligne vient de `_state.ResetsAt`, donnée serveur déjà alimentée par `Apply`. Ce n'est ni un littéral ni une estimation. Le statut est ✓ FLOWING.

### Contrôles comportementaux

L'exe n'est pas lancé, conformément à la consigne. Les tests existent : `BraisesGeometrieTests` (4), `CadranBraisesTests` (10, dont 16,2° et rien à midi ni à 72°, demi-allumage, attente, hebdo uniforme, flèche fixe hors anneau non cliquable, absence d'animation, 3e ligne en galerie, % inchangé à 2 lignes, ligne masquée sans heure), plus des tests dans `WindowGaugeViewModelTests` et `CadranBindingTests`. Je n'ai pas relancé la suite. La porte annoncée par l'orchestrateur est build `-warnaserror` à 0 avertissement et 1893/1893.

### Couverture des exigences

| Exigence | Plan | Description | Statut | Preuve |
|----------|------|-------------|--------|--------|
| BRA-01 | 41-01 | 20 braises de 15 min en 5 groupes + flèche fixe à midi `TickReset` | ✓ SATISFAITE | vérités 1 et 2 |
| BRA-02 | 41-02 | « ↻ HH:MM » en mode temps sous les deux comptes à rebours | ✓ SATISFAITE | vérité 3 |

Aucune exigence orpheline : REQUIREMENTS.md n'associe à la phase 41 que BRA-01 et BRA-02, toutes deux déclarées dans les plans.

### Anti-patterns

Aucun TODO, stub ni donnée codée en dur trouvé dans les fichiers de la phase. Le `FontSize="10.5"` littéral est justifié par un commentaire, car aucun token de cadran n'existe à ce corps. Le contrat de design § 3 demande « 10,5 ».

### Vérification humaine

Aucune n'est requise pour ce verdict. La revue visuelle (lisibilité du vide à R66, position de la flèche à l'échelle 1) relève de la DESIGN-REVIEW après la phase 42, et n'est pas un gap.

### Résumé des écarts

Aucun écart. Les critères de succès sont tenus dans le code, branchés et couverts par des tests.

---

_Vérifié : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
