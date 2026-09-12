# Éléments reportés — phase 18

## 18-05 — Coût en temps de `BuildReportAsync` en test (hors périmètre du plan)

**Constat mesuré.** La suite complète passe de **56 s** (baseline 628 tests) à **~2 min 35** après le
plan 18-05, pour 5 tests de plus seulement. La cause n'est pas la sonde : c'est que chaque test de
`DiagnosticServiceTests` exécute `BuildReportAsync`, qui fait deux choses coûteuses et **non injectables** :

1. `FindTokenVaults` balaie `%APPDATA%` et `%LOCALAPPDATA%` sur 3 niveaux de profondeur à la recherche
   des `config.json` contenant `oauth:tokenCache` ;
2. un poll **one-shot** réel de `DesktopUiaSessionSource` (arbre UI Automation de l'app Claude).

Les 3 tests préexistants payaient déjà ce prix (~20 s chacun) ; les 4 tests mandatés par le plan 18-05
le paient à leur tour. Le coût est donc **linéaire et connu**, pas une régression de conception.

**Pourquoi ce n'est PAS corrigé ici.** Rendre ces deux découvertes injectables (une interface de
découverte de coffres + l'`ISessionSource` déjà existante passée en paramètre) change la signature de
`DiagnosticService`, ce que le plan 18-05 interdit explicitement (« PAS de changement de signature —
10 sites de construction »), et touche une section du rapport hors périmètre.

**Candidat.** Phase 20 (« Honnêteté visible — cadran & diagnostic »), qui rouvre légitimement
`DiagnosticService` pour EXA-03/EXA-06 : y injecter les deux sources de découverte ferait tomber le coût
de ~20 s à quelques millisecondes par test.
