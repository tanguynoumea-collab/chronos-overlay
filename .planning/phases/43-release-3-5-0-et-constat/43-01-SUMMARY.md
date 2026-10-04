---
phase: 43-release-3-5-0-et-constat
plan: 01
subsystem: sources / transcripts
tags: [DS3-01, DS3-02, doctrine, transcripts, documentation]
requires: [42.4-02 (SourceActiviteMemoisee sans repli)]
provides:
  - racine des transcripts présente mais inaccessible = passe en échec = « Indisponible »
  - énumérateur des *.jsonl injectable (ctor internal de TranscriptActivityProvider)
  - limites DS3-01 / DS3-02 écrites au §4 de docs/data-sources.md, sous garde
affects: [43-02 (doc 3.5), 43-03 (release), 43-04 (constat)]
tech-stack:
  added: []
  patterns: [injection d'un Func<string, IEnumerable<string>> comme seam de test, ACL Deny réversible sur dossier temp]
key-files:
  created:
    - tests/Chronos.Tests/TranscriptRacineInaccessibleTests.cs
  modified:
    - src/Chronos/Services/TranscriptActivityProvider.cs
    - src/Chronos/Services/ChronosPaths.cs
    - tests/Chronos.Tests/GardesReliquatsTests.cs
    - docs/data-sources.md
decisions:
  - "Racine absente = journal vide (machine sans Claude Code), hypothèse écrite ; racine présente mais inaccessible = exception, plus aucun catch dans EnumerateJsonl"
  - "CLAUDE_CONFIG_DIR non lu (D-01) : limite écrite en remarks de ChronosPaths.Default et au §2/§4 de data-sources"
  - "Test ACL réel retenu : le refus ListDirectory posé par le propriétaire, sans droits admin, laisse Directory.Exists vrai et fait lever l'énumération dès sa construction"
metrics:
  duration: ~25 min
  completed: 2026-10-04
  tasks: 2
  files: 5
---

# Phase 43 Plan 01 : DS3-02, une racine inaccessible ne certifie jamais « encore valide » — Summary

`TranscriptActivityProvider.EnumerateJsonl` n'avale plus les exceptions. Une racine des transcripts présente mais refusée
fait échouer la passe, et la tête la convertit en « Indisponible ». Une racine absente rend toujours un journal vide.
Les limites DS3-01 et DS3-02 sont écrites au §4 de `docs/data-sources.md`, sous garde.

## Constat RED / GREEN par test

| Test | RED (e3da912) | GREEN (757b2e5) |
|------|---------------|-----------------|
| `Une_racine_presente_dont_l_enumeration_leve_tout_de_suite_fait_echouer_la_passe` | échec (journal vide rendu) | vert |
| `Une_racine_presente_en_partage_refuse_fait_echouer_la_passe` (IOException) | échec | vert |
| `Une_racine_refusee_par_ACL_fait_echouer_la_passe` (vrai disque, ctor public) | échec (« No exception was thrown ») | vert |
| `Via_la_doctrine_une_racine_inaccessible_ne_certifie_jamais_un_releve_exact` | échec (EncoreValide) | vert |
| `GardesReliquatsTests.Les_limites_DS3_01_et_DS3_02_sont_ecrites` | échec (§4 non écrit) | vert |
| `Une_levee_pendant_l_iteration_paresseuse_fait_echouer_la_passe` | vert (la levée paresseuse remontait déjà) | vert |
| `Une_racine_absente_rend_un_journal_vide_sans_exception` | vert | vert |
| `Via_la_doctrine_une_racine_absente_laisse_l_hypothese_encore_valide` (témoin D-02) | vert | vert |

Filtre RED : 5 échecs, 23 réussites sur 28. Les 5 échecs sont exactement les tests RED attendus. Tous les tests existants
de `TranscriptActivityProviderTests` et `GardesReliquatsTests` étaient verts.

## Le test ACL optionnel : écrit

Je l'ai d'abord essayé dans un dossier temporaire, avec PowerShell. Le propriétaire (l'utilisateur courant, sans droits admin)
se pose une règle `Deny ListDirectory`. Résultat : `Directory.Exists` reste vrai, et `Directory.EnumerateFiles` lève
`UnauthorizedAccessException` dès sa construction, avant toute itération. Cela confirme l'analyse du plan. La règle se
retire ensuite et le dossier se supprime sans problème.

Le test utilise `FileSystemAclExtensions` et `WindowsIdentity`, fournis avec net8.0-windows : aucun package ajouté.
La règle est retirée en `finally`, avant la suppression faite par `Dispose`. Après les exécutions, il ne reste aucun
dossier `ChronosRacine*_` de ce plan sous `%TEMP%`.

## Vérification finale

- `dotnet build Chronos.sln -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : **2 217 réussis, 0 échec, 1 ignoré, total 2 218**.
- Critères d'acceptation vérifiés :
  - « jamais d'exception » : 0 occurrence ; `catch (UnauthorizedAccessException) { return Array.Empty<string>(); }` : 0 ;
  - `DS3-02` : 4 occurrences dans le provider ; `new DedupUsage()` : 1 ;
  - `CLAUDE_CONFIG_DIR` : 2 occurrences dans data-sources.md, présent dans ChronosPaths.cs ; `GetEnvironmentVariable` : aucune ;
  - « Reconnexion avec un autre compte » : 1 occurrence ; aucun CR dans data-sources.md.
- Fichiers inchangés : `App.xaml.cs`, `SourceActiviteMemoisee.cs`, `LastExactUsageProvider.cs`, `DoctrineFraicheur.cs`.
  Aucun exe lancé.

## Écarts par rapport au plan

- **Test ACL optionnel écrit.** Le plan le prévoyait seulement si un essai préalable était concluant ; l'essai l'a été.
  Le commentaire du seam a été ajusté en conséquence : il ne dit plus que l'ACL « n'est pas manipulable ».
- **DS3-01 vérifié dans le code.** `MainViewModel.ReconnecterAsync` (la pastille) n'appelle pas
  `OublierDernierReleve`, au contraire de `LoginClaude`. La borne d'environ 5 min vient du frein de la sonde
  (`CadenceNominale` = 300 s). La puce du §4 le précise.
- **Tableau du §3 non modifié.** La cellule « Indisponible » (« activité impossible à établir ») couvre déjà le cas.

Aucune autre déviation (Règles 1-4 non déclenchées).

## Known Stubs

Aucun.

## Commits

- e3da912 — test(43-01): racine des transcripts inaccessible jamais certifiante, limites DS3-01/DS3-02 sous garde (RED)
- 757b2e5 — fix(43-01): une racine des transcripts inaccessible ne certifie plus « encore valide » ; limites DS3-01/DS3-02 documentées

## Self-Check: PASSED

- FOUND: tests/Chronos.Tests/TranscriptRacineInaccessibleTests.cs
- FOUND: e3da912, 757b2e5
