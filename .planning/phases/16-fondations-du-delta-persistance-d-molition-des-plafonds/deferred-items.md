# Éléments différés — Phase 16

Découvertes hors périmètre relevées pendant l'exécution, NON corrigées (SCOPE BOUNDARY).

## [16-01] Barre de progression de STATE.md figée à 0 %

- **Constat :** `gsd-tools state update-progress` renvoie `{"updated":true,"percent":57,"completed":4,"total":7}`
  mais `.planning/STATE.md` conserve `percent: 0` en frontmatter et `Progress: [░░░░░░░░░░] 0% (0/6 phases)`
  dans le corps — alors que `completed_phases: 1`.
- **Nature :** décalage de format entre l'outil (compte les PLANS) et la ligne présente dans STATE.md
  (compte les PHASES). Pré-existant : STATE.md affichait déjà `0% (0/6 phases)` avec `completed_phases: 1`
  avant le plan 16-01.
- **Pourquoi non corrigé :** aucun rapport avec les fichiers touchés par 16-01 ; STATE.md est écrit par
  l'outillage GSD, une correction manuelle serait écrasée au prochain `update-progress`.
- **Action suggérée :** à traiter au niveau de l'outillage GSD, pas dans une phase produit.

## [16-01] 2 avertissements xUnit2031 pré-existants

- `tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs:345` et `:364` — `Assert.Single` précédé d'un `Where`.
- Présents avant le plan 16-01, dans un fichier non touché. Purement cosmétique (analyseur xUnit).
