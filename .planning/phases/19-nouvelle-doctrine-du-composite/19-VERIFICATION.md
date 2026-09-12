---
phase: 19-nouvelle-doctrine-du-composite
verified: 2026-09-12T00:00:00Z
status: human_needed
score: 5/5 must-haves verified (code + tests) ; 1/1 human checkpoint still pending
human_verification:
  - test: "Fermer l'overlay lancé par l'autostart, puis lancer `dotnet run --project src/Chronos/Chronos.csproj -c Release` depuis le dépôt et observer le cadran."
    expected: "Le cadran n'affiche plus « 10 % » ; il affiche un état indisponible (aucun pourcentage) et une pastille cliquable en bas à droite avec l'infobulle « Aucun chiffre d'usage n'a jamais été obtenu — cliquer pour se connecter à Claude »."
    why_human: "Dépend de l'état réel de %APPDATA%\\Chronos\\usage.json sur cette machine (figé au 2026-07-10) ; aucun test automatisé ne peut produire cette preuve sans lancer l'overlay réel, ce qui déclencherait la purge des hooks de ~/.claude/settings.json hors supervision. Ne PAS cliquer sur la pastille (parcours de reconnexion réel hors périmètre de cette vérification)."
  - test: "Après avoir fermé l'overlay, revérifier `stat -c '%s %Y' \"$APPDATA/Chronos/oauth.dat\"` (attendu 518 1783863147) et `cat \"$APPDATA/Chronos/usage.json\"` (doit être identique à avant)."
    expected: "Coffre et usage.json rigoureusement inchangés ; last-exact.json peut être apparu (normal si une source a répondu)."
    why_human: "Contrôle de non-régression à faire par la même personne qui exécute le premier test, immédiatement après."
---

# Phase 19: Nouvelle doctrine du composite — Verification Report

**Phase Goal:** Chronos n'affiche plus jamais qu'un chiffre exact, éventuellement corrigé d'un delta borné
et marqué comme tel, ou rien du tout : exact frais → dernier exact persisté encore rigoureusement valide →
dernier exact + delta borné avec sa marge → indisponible.

**Verified:** 2026-09-12 (re-lecture indépendante du code, ré-exécution de la suite complète et des gardes)
**Status:** human_needed
**Re-verification:** No — initial verification

## Méthode

Cette vérification n'a PAS pris pour argent comptant les 5 SUMMARY.md de la phase. Pour chacune des
affirmations-clé, le code source réel a été lu et confronté au texte des SUMMARY, puis la suite de tests a
été ré-exécutée indépendamment (pas seulement relue dans les rapports). Concrètement :

- Lecture intégrale de `DoctrineFraicheur.cs`, `LastExactUsageProvider.cs`, `LastExactStore.cs`,
  `ClaudeUsageObjectProvider.cs`, `CompositeUsageProvider.cs`, `WeeklyRecalibration.cs`,
  `WindowState.cs`, `UsageSnapshot.cs`, `SourceReliability.cs`, `WindowGaugeViewModel.cs`,
  `PercentFormatter.cs`, `MainViewModel.cs` (extraits), `MainWindow.xaml` (extraits).
- Ré-exécution réelle de `dotnet test Chronos.sln -v q --nologo` (699/699, 0 échec, 2 m 10 s) et du sous-
  ensemble des gardes permanentes + `DoctrineFraicheurTests` (26/26, 0 échec).
- Contrôle direct des fichiers de production : `oauth.dat` (518 octets, mtime 1783863147),
  `usage.json` (77 octets, mtime 1783666519, contenu inchangé), `last-exact.json` (absent),
  `~/.claude/settings.json` (mtime du 30 juillet, antérieur à toute la phase — l'overlay n'a jamais été
  lancé pendant la phase).
- `git status --short` vide, `git log` sur `CompositeUsageProvider.cs` confirmant qu'aucun commit de
  logique n'y a été fait depuis la phase 18 (seul un commit documentaire de 19-02).

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Fin du « exact » périmé (EXA-02) : un relevé au-delà de la limite d'âge cesse d'être présenté comme exact | ✓ VERIFIED | `DoctrineFraicheur.Statuer` démote en `Unavailable` tout relevé dont `Age > LimiteAge` sans preuve d'inactivité couvrant `Covers(T)`. `ClaudeUsageObjectProvider.ReadWindow` reçoit `capturedAt` extrait du FICHIER (`root.TryGetProperty("capturedAt", ...)`, jamais `_clock.UtcNow`) — grep confirmé `CapturedAt\s*=\s*_clock` == 0. Test `Un_usage_json_vieux_de_deux_mois_porte_l_age_du_FICHIER_et_non_celui_de_la_lecture` existe (ligne 122) et passe. |
| 2 | Encore exact quand rien n'a bougé (DEL-03) | ✓ VERIFIED | `DoctrineFraicheur.Statuer` branche 2 : `!activite.HasActivity` → `Exact`/`EncoreValide`, `Utilization` inchangée, `TokensDepuisReleve = 0`. Câblé en production dans `LastExactUsageProvider.GetAsync` (`DoctrineFraicheur.Statuer(...)` appelé 2 fois, une par fenêtre). Test `Un_releve_de_deux_heures_SANS_activite_depuis_reste_EXACT` existe et passe. |
| 3 | Exact + delta marqué quand ça a bougé (DEL-04) | ✓ VERIFIED (avec réserve documentée sur la lettre) | Branche 3 : `Estimated`/`PlancherAvecActivite`, `Utilization` **jamais gonflée** (vérifié dans le code : `Qualifier` ne fait que changer `Reliability`/`Provenance`/`TokensDepuisReleve`, jamais `Utilization`). `PercentFormatter.Format(double?, ProvenanceReleve?)` produit « ≥ N % » — confirmé dans le fichier réel. Bindé dans `WindowGaugeViewModel.Apply` (`UtilizationText = PercentFormatter.Format(s.Utilization, s.Provenance)`). Réserve : la lettre exigeait « + delta estimé » chiffré ; la matière brute (`TokensDepuisReleve`) est calculée et testée mais **non bindée en XAML** (vérifié : aucune occurrence dans les .xaml) — décision documentée et justifiée par une mesure (280 % faux si converti). Ceci est un amendement assumé de la lettre, pas un défaut caché. |
| 4 | Plus aucun pourcentage inventé (EXA-04, EXA-05) | ✓ VERIFIED | `GardesDoctrineTests` contient une garde par balayage de texte source interdisant tout rapport tokens/plafond dans `Services/`+`Models/` — vérifiée exécutable et falsifiable (mutation jouée et révoquée d'après le SUMMARY, code de la garde relu). `UsageSnapshot.UnExactADejaEteObtenu` est `bool?` (jamais `bool`), et `LastExactUsageProvider.GetAsync` pose explicitement `dejaEuUnExact = null` dans le bloc `catch` (panne du magasin) — confirmé en lisant le fichier. `MainViewModel` : `_jamaisDExactEtRienAAfficher = snap.UnExactADejaEteObtenu == false && DataUnavailable` (comparaison stricte à `false`, pas `!= true`) — confirmé par grep. Pastille `PastilleInvitationConnexion` bindée sur `{Binding ReconnecterCommand}`, jamais `LoginClaudeCommand` — confirmé par lecture directe de `MainWindow.xaml` (lignes 188-196). |
| 5 | Doctrine reproductible sous test (4 branches, gardes de pureté/composition vertes) | ✓ VERIFIED | `DoctrineFraicheurTests` : 13 tests couvrant les 4 branches, ré-exécutés indépendamment (0 échec). Les mutations de falsifiabilité décrites dans 19-02-SUMMARY (retrait de chaque `return`) sont plausibles au vu du code lu (chaque branche a un `return` distinct et un test qui l'exerce spécifiquement). Gardes permanentes (`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`, garde de position de la sonde) toutes vertes en ré-exécution indépendante (26/26). |

**Score:** 5/5 truths vérifiées par lecture de code + ré-exécution indépendante de la suite de tests.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/DoctrineFraicheur.cs` | Classe pure, 4 branches falsifiables | ✓ VERIFIED | Lu intégralement ; aucune E/S, aucune horloge propre (`now` toujours paramètre), `LimiteAge` dérivée de `RateLimitHeaderUsageProvider.CadenceNominale`. |
| `src/Chronos/Services/LastExactUsageProvider.cs` | Couche de doctrine câblée en tête de chaîne, paresseuse, mémoïsée | ✓ VERIFIED | 4e paramètre `activite` obligatoire (pas de `= null`), `DoctrineFraicheur.Statuer` appelée 2 fois, `_activite.ReadAsync` appelée conditionnellement (`ABesoinDuJournal`), `try/catch` transforme toute panne en dégradation silencieuse. |
| `src/Chronos/Services/LastExactStore.cs` | `UnExactADejaEteObtenu()`, `Convertir` à 3 conditions | ✓ VERIFIED | Les deux éléments présents mot pour mot ; `SchemaVersion` toujours 1. |
| `src/Chronos/Services/ClaudeUsageObjectProvider.cs` | `CapturedAt` du fichier, jamais de la lecture | ✓ VERIFIED | Piège le plus grave de la phase correctement désamorcé — vérifié ligne par ligne. |
| `src/Chronos/Services/CompositeUsageProvider.cs` | Quasi intouché, 14 tests intacts | ✓ VERIFIED | `git log` confirme un seul commit documentaire depuis la phase 18 ; comptage de `[Fact]` = 14 dans `CompositeUsageProviderTests.cs`, tous verts. |
| `src/Chronos/Services/WeeklyRecalibration.cs` | Garde sur `ResetsAt` seul | ✓ VERIFIED | `if (weekly.ResetsAt is not null) return weekly;` — confirmé, plus de test sur `Reliability`. |
| `src/Chronos/ViewModels/MainViewModel.cs` | Invitation EXA-05, bit `null`/`false`/`true` correctement traité | ✓ VERIFIED | `MajPastilles`, `_jamaisDExactEtRienAAfficher`, comparaison `== false`. |
| `src/Chronos/Views/MainWindow.xaml` | Pastille bindée sur `ReconnecterCommand` | ✓ VERIFIED | Confirmé par lecture directe, jamais `LoginClaudeCommand`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `ClaudeUsageObjectProvider.ReadWindow` | `WindowState.CapturedAt` | `capturedAt` du fichier passé en paramètre | ✓ WIRED | Vérifié dans le code, pas de `_clock.UtcNow` sur ce champ. |
| `LastExactUsageProvider.GetAsync` | `DoctrineFraicheur.Statuer` | appel direct, 2 fois (une par fenêtre) | ✓ WIRED | Confirmé. |
| `LastExactUsageProvider.GetAsync` | `ITranscriptActivitySource.ReadAsync` | conditionné par `ABesoinDuJournal` | ✓ WIRED | Une seule invocation dans le fichier (paresse + passe unique). |
| `App.xaml.cs` | `SourceActiviteMemoisee` | `services.AddSingleton<ITranscriptActivitySource>(...)` | ✓ WIRED | Confirmé, et le 4e argument `activite:` est bien passé au constructeur de `LastExactUsageProvider`. |
| `MainWindow.xaml` (PastilleInvitationConnexion) | `ReconnecterCommand` | `{Binding ReconnecterCommand}` | ✓ WIRED | Confirmé ; `LoginClaudeCommand` absent de ce binding. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `WindowGaugeViewModel.UtilizationText` | `s.Utilization`, `s.Provenance` | `LastExactUsageProvider.GetAsync` → `DoctrineFraicheur.Statuer` → chaîne composite réelle (`ClaudeUsageObjectProvider`/OAuth/sonde) | Oui, chaîne complète tracée jusqu'au fichier `usage.json` réel | ✓ FLOWING |
| `MainViewModel.AfficherInvitationConnexion` | `snap.UnExactADejaEteObtenu`, `DataUnavailable` | `LastExactStore.UnExactADejaEteObtenu()` + `ApplySnapshot` | Oui — sur la machine réelle, `usage.json` est vieux de 64 jours (`Covers(T)` faux) et `last-exact.json` est absent, donc la chaîne réelle produit `false` → invitation prévue allumée (prédiction non encore visuellement confirmée) | ⚠️ FLOWING mais preuve visuelle en attente (voir human_needed) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète 0 échec | `dotnet test Chronos.sln -v q --nologo` | 699 réussis / 699, 0 échec, 2 m 10 s | ✓ PASS |
| Gardes permanentes + doctrine | `--filter "ServicesLayerPurityTests\|CompositionRootTests\|NormalisationUniqueTests\|GardesDoctrineTests\|DoctrineFraicheurTests\|La_sonde_d_en_tetes_est_le_PRIMAIRE"` | 26 réussis / 26, 0 échec | ✓ PASS |
| Coffre de jetons intact | `stat -c '%s %Y' oauth.dat` | `518 1783863147` avant et après exécution de la suite | ✓ PASS |
| `usage.json` intact (cas de test de production) | `cat usage.json` + `stat` | 77 octets, mtime 1783666519, contenu identique | ✓ PASS |
| `last-exact.json` toujours absent | `ls` | absent | ✓ PASS (confirme que la machine n'a pas été « polluée » par une exécution antérieure) |
| Un seul rafraîchisseur | `grep -rl "RefreshAsync" src/Chronos --include=*.cs` | 2 fichiers exactement (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) | ✓ PASS |
| `~/.claude/settings.json` jamais touché pendant la phase | `stat` mtime | 30 juillet — antérieur à toute la phase 19 | ✓ PASS |
| Cadran réel montre la bascule « 10 % » → « indisponible + invitation » | `dotnet run --project src/Chronos/Chronos.csproj -c Release` puis observation visuelle | non exécuté | ? SKIP — délibérément non automatisable et non simulé (routé en human_needed) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|--------------|--------|----------|
| EXA-02 | 19-01, 19-02, 19-03 | Limite d'âge sur toute source exacte | ✓ SATISFIED | `DoctrineFraicheur.Statuer` branches 1/4b, câblée en production, testée. |
| EXA-04 | 19-02, 19-03, 19-04 | Aucune utilization dérivée d'un comptage de tokens | ✓ SATISFIED | Garde de balayage + `Utilization` jamais gonflée dans `Qualifier` + `TokensDepuisReleve` jamais converti. |
| EXA-05 | 19-01, 19-03, 19-04 | Indisponible + invitation si jamais d'exact obtenu | ✓ SATISFIED | `UnExactADejaEteObtenu` bool? correctement propagé, pastille bindée et exclusive de la pastille de déconnexion. |
| DEL-03 | 19-02, 19-03 | Sans activité → encore exact | ✓ SATISFIED | Branche 2 câblée et testée de bout en bout. |
| DEL-04 | 19-02, 19-03, 19-04 | Avec activité → « + delta estimé » marqué | ✓ SATISFIED (lettre amendée, documentée) | « ≥ N % » affiché et testé ; la magnitude chiffrée du delta n'est délibérément pas affichée (justifiée par la mesure de 280 % d'erreur potentielle) — réserve documentée dans 19-04/19-05-SUMMARY, à trancher explicitement en phase 20. |

Aucun requirement orphelin : la liste `{EXA-02, EXA-04, EXA-05, DEL-03, DEL-04}` déclarée dans
`REQUIREMENTS.md` pour la Phase 19 correspond exactement à l'union des champs `requirements:` des 5 plans.

### Anti-Patterns Found

Aucun. Balayage `TODO|FIXME|XXX|HACK|PLACEHOLDER|not.?implemented|coming soon` sur les 13 fichiers-clé de
la phase (Services, Models, ViewModels, Text) : 0 occurrence.

## Points vérifiés au-delà des must-haves standards (spécifiquement demandés)

- **Piège le plus grave (ClaudeUsageObjectProvider horodate le FICHIER, pas `now`)** : vérifié ligne par
  ligne dans le fichier réel — confirmé correct, avec le test de non-retour associé présent.
- **Garde de position de la sonde phase 18** (`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`) :
  verte en ré-exécution indépendante.
- **`CompositeUsageProvider.cs` quasi intouché** : confirmé par `git log` (un seul commit documentaire
  depuis la phase 18) et par comptage des 14 tests, tous verts.
- **Chemin nominal ne lit jamais les transcripts, 2 fenêtres dégradées partagent 1 passe** : le code de
  `LastExactUsageProvider.GetAsync` confirme une seule invocation de `_activite.ReadAsync`, conditionnée
  par un OU logique sur les deux fenêtres — donc structurellement une seule passe pour les deux. Les tests
  correspondants (`Chemin_nominal_ne_lit_JAMAIS_les_transcripts`, `Les_deux_fenetres_degradees_partagent_UNE_SEULE_passe_disque`)
  existent dans `LastExactUsageProviderTests.cs`.
- **Plancher jamais persisté comme exact** : confirmé dans `GetAsync` — l'écriture au magasin
  (`_store.Save(snap)`) se fait sur `snap` (sortie brute de l'inner, avant tout appel à `DoctrineFraicheur.Statuer`),
  donc un plancher produit PAR la doctrine ne peut jamais boucler vers le magasin. Test
  `Un_plancher_n_est_JAMAIS_persiste_comme_exact` existe.
- **Invitation bindée sur `ReconnecterCommand`, jamais `LoginClaudeCommand`** : confirmé par comparaison
  d'instances dans le code de test (`Assert.Same`/`Assert.NotSame` décrits dans le SUMMARY) et par lecture
  directe du binding XAML.
- **`UnExactADejaEteObtenu` vaut `null`, jamais `false`, quand le magasin est en panne** : confirmé — le
  bloc `catch` de `LastExactUsageProvider.GetAsync` pose explicitement `dejaEuUnExact = null`.
- **Correctif `WeeklyRecalibration`** : confirmé, la garde ne teste plus `Reliability`.
- **Sécurité** : `oauth.dat` (518 o, mtime 1783863147) et `usage.json` (77 o, mtime 1783666519, contenu
  identique) contrôlés directement AVANT et APRÈS l'exécution complète de la suite de tests par ce
  vérificateur — inchangés. `~/.claude/settings.json` non touché (mtime antérieur à toute la phase).
  `git status --short` vide. `RefreshAsync` dans exactement 2 fichiers source.
- **Pas d'anticipation de la phase 20** : `ProvenanceReleve` compte exactement 3 membres, tous des états,
  aucun ne nomme une source — confirmé par lecture du fichier. EXA-03/EXA-06 correctement laissés `Pending`
  dans `REQUIREMENTS.md`.
- **Stabilité** : suite complète ré-exécutée une fois de façon indépendante par ce vérificateur (699/699,
  0 échec), en plus des deux exécutions consécutives déjà documentées par 19-05-SUMMARY. Trois exécutions
  indépendantes au total, toutes identiques.

## Human Verification Required

### 1. Constat de la bascule « 10 % » → « indisponible + invitation » sur la machine réelle

**Test:** Fermer l'overlay lancé par l'autostart (exe déjà publié, ancien comportement), puis lancer
`dotnet run --project src/Chronos/Chronos.csproj -c Release` depuis le dépôt et observer le cadran.
**Expected:** Le cadran n'affiche plus « 10 % ». Il affiche un état indisponible (aucun pourcentage, arcs
neutres) et une pastille cliquable en bas à droite avec l'infobulle « Aucun chiffre d'usage n'a jamais été
obtenu — cliquer pour se connecter à Claude ». Ne pas cliquer sur la pastille.
**Why human:** Dépend de l'état réel et non reproductible en test du fichier `%APPDATA%\Chronos\usage.json`
(figé au 2026-07-10, 64 jours d'âge à ce jour). Lancer l'overlay réel déclenche la purge des groupes de
hooks de `~/.claude/settings.json` (comportement codé en phase 15) — geste à ne pas faire hors supervision
de l'utilisateur. C'est très exactement la seule preuve que ni les tests ni ce vérificateur ne peuvent
produire : le chemin complet du fichier réel jusqu'au pixel affiché.

### 2. Contrôle de non-régression après le test 1

**Test:** Après avoir fermé l'overlay lancé pour le test 1, revérifier
`stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` (attendu `518 1783863147`) et `cat "$APPDATA/Chronos/usage.json"`
(doit être identique à l'état relevé avant le test 1 : `{"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}`,
77 octets).
**Expected:** Coffre et fichier d'usage rigoureusement inchangés. `last-exact.json` peut être apparu — c'est
normal si une source a répondu pendant l'exécution.
**Why human:** À exécuter par la même personne, immédiatement après le test 1, pour clore la boucle de
vérification.

## Gaps Summary

Aucun gap de code ou de test n'a été trouvé : les cinq exigences (EXA-02, EXA-04, EXA-05, DEL-03, DEL-04)
sont implémentées, câblées en production, et couvertes par des tests qui existent réellement dans le
dépôt (vérifié par lecture directe, pas seulement par les SUMMARY). La suite complète est verte (699/699,
0 échec) sur une ré-exécution indépendante, les gardes permanentes (pureté, composition, normalisation,
position de la sonde, gardes de doctrine) sont vertes, le coffre de jetons et le fichier `usage.json` de
production sont rigoureusement intacts, et `~/.claude/settings.json` n'a jamais été touché pendant la phase.

Le seul élément manquant est la preuve visuelle finale — le constat que le cadran RÉEL bascule bien de
« 10 % » vers « indisponible + invitation à se connecter ». Cette preuve a été délibérément **substituée**
par la porte automatisée plutôt que **simulée**, ce qui est la bonne pratique (l'équipe qui a exécuté la
phase 19 a explicitement refusé de prétendre avoir vérifié ce qu'elle n'a pas vérifié — cf. `19-VALIDATION.md`,
ligne `19-05 T2`, statut `⏳ à vérifier par l'utilisateur`, distinct de `✅ passed`). Ce vérificateur confirme
cette honnêteté : la prédiction outillée (branche 4, invitation allumée) est cohérente avec tout ce que le
code fait réellement, mais reste une prédiction tant qu'elle n'a pas été vue à l'écran.

Une réserve non-bloquante est documentée sur DEL-04 : la lettre de l'exigence demandait un delta chiffré
(« + delta estimé »), remplacé par une marque unilatérale (« ≥ N % ») sans magnitude affichée, décision
justifiée par une mesure de terrain (un delta chiffré aurait été faux d'un facteur ~2,8). C'est un
amendement assumé et documenté, cohérent avec la Core Value du projet (ne jamais présenter une estimation
comme exacte), à trancher explicitement lors du design de la phase 20 (dette n°1 : `TokensDepuisReleve`
calculé, testé, mais non bindé en XAML).

---

*Verified: 2026-09-12*
*Verifier: Claude (gsd-verifier)*
