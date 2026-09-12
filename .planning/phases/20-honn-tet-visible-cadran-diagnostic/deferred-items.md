# Éléments différés — phase 20

## Découvert pendant 20-01 T2 (hors périmètre du plan)

**`tests/Chronos.Tests/OverlayWindowConfigTests.cs` monte encore son VM sur les chemins réels du
profil utilisateur** (4 usages, l. 21/22/26/29 — exactement le motif que 20-01 T2 vient de retirer de
`CadranBindingTests`). Cette classe construit elle aussi une `MainWindow` complète, donc son
`MainViewModel` lit `settings.json` de la machine dans son constructeur.

- **Pourquoi non corrigé ici** : le plan 20-01 borne explicitement la tâche 2 à `CadranBindingTests`.
  C'est une dette PRÉEXISTANTE, non causée par les changements de ce plan.
- **Risque concret** : si un plan ultérieur de la phase 20 ajoute une assertion de style ou de mode à
  `OverlayWindowConfigTests`, elle sera verte par accident sur cette machine.
- **Correctif** : recopier le helper `TempPaths()` et le motif à une seule variable `paths`
  (préfixe de dossier `"ChronosOverlayTest_"`). Coût estimé : quelques minutes.
- **Non concerné** : `CompositionRootTests.cs:43` utilise volontairement
  `ChronosPaths.Default() with { UsageFile = tmpUsage }` et documente déjà la garde anti-accident.
