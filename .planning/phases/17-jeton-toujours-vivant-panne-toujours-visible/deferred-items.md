# Éléments différés — Phase 17

Découvertes HORS PÉRIMÈTRE des plans de la phase. Aucune n'a été corrigée ici (règle de portée :
n'auto-corriger que ce que la tâche courante a causé).

## Découvert au plan 17-03

### 1. La barre de progression de `STATE.md` reste figée à 0 %

- **Constat :** `gsd-tools state update-progress` rend `{ percent: 83, bar: "[████████░░] 83%" }` (10/12
  plans) mais n'écrit ni le champ `percent:` du frontmatter (resté à `0`) ni la ligne
  `Progress: [░░░░░░░░░░] 0% (0/6 phases)`. Les deux métriques ne parlent d'ailleurs pas de la même
  chose : l'outil compte des **plans**, la ligne de texte annonce des **phases**.
- **Pré-existant :** oui — `percent: 0` figurait déjà dans `STATE.md` avec `completed_plans: 9` au terme
  du plan 17-02, et la ligne `0% (0/6 phases)` est inexacte depuis la clôture des phases 15 et 16.
- **Impact :** purement cosmétique. Aucun effet sur l'exécution, les tests ou le produit.
- **Pourquoi non corrigé ici :** éditer `STATE.md` à la main contournerait l'outil qui en est
  propriétaire, et le décalage réapparaîtrait au plan suivant. La correction appartient à l'outillage
  GSD, pas au code de Chronos.

### 2. `DiagnosticService.cs:220` remonte encore sur le `grep` « aucun jeton en clair »

- **Constat :** la ligne écrit le **nom** du champ (`claudeAiOauth.accessToken`) suivi de
  « PRÉSENT »/« absent », **jamais la valeur**. Le critère de vérification de chaque plan de la phase la
  signale donc systématiquement.
- **Pré-existant :** oui — déjà relevé aux plans 17-01 et 17-02, jamais touché.
- **Impact :** aucun risque de fuite. Le critère `grep` est simplement plus large que son intention.
- **Pourquoi non corrigé ici :** `DiagnosticService.cs` est hors du périmètre de 17-03 ; le plan 17-04
  le modifie (« diagnostic qui nomme l'état réel ») et pourra reformuler cette ligne à cette occasion.
