# Phase 30 — Éléments reportés (hors périmètre)

## 1. Test sensible à la charge de la machine : `RefreshOrchestratorTests.PeriodicTimer_declenche_GetAsync_sans_evenement_watcher`

- **Relevé pendant :** 30-03, tâche 3 (2026-09-26, vers 09:15Z).
- **Constat :** sur trois exécutions de la suite complète faites pendant un ralentissement général de la machine (suite
  en 1,4 à 3,1 min au lieu de 11 s ; des tests triviaux à 1 s, un test d'E/S à 1 min 29 s), ce test a échoué une fois
  (`GetCount attendu >= 2 via PeriodicTimer`). Il attend 2 s qu'un minuteur de 50 ms déclenche un second appel.
- **Pourquoi ce n'est pas 30-03 :** test de la phase 04 (`0b0481b`), sans lien avec le moniteur ni le premier plan ; le
  même code, sans modification, passe ensuite cinq fois de suite en 11 à 16 s. La charge venait de la machine (nombreux
  processus `dotnet` d'autres sessions, un agent de la phase 31 committait en parallèle).
- **Piste (non appliquée) :** allonger l'attente ou compter des ticks sur une horloge injectée plutôt que sur le temps
  réel. À décider hors de cette phase.
