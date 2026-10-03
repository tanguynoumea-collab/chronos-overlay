---
phase: 36-socle
verified: 2026-10-03T00:00:00Z
status: passed
score: 4/4 critères de réussite vérifiés
---

# Phase 36 : Socle — Rapport de vérification

**Objectif de la phase :** rendre sûres toutes les suppressions du milestone. Un `settings.json` qui contient une valeur inconnue ne perd que cette valeur. Un `--statusline` (après la phase 37) ou tout `--xxx` inconnu sort en silence (code 0) avant le verrou mono-instance.
**Vérifié le :** 2026-10-03
**Statut :** passed
**Re-vérification :** non, vérification initiale.

Le code a été relu directement. Les SUMMARY n'ont pas servi de preuve. La porte de vague (build à 0 avertissement, 1756/1756 tests) a été fournie par l'orchestrateur et n'a pas été relancée. L'exe n'a pas été lancé.

## Vérités observables (critères de réussite de la ROADMAP)

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Une valeur fautive ne coûte qu'elle-même. Thème, coin, géométries et `InnerStatusLineCommand` sont conservés et réécrits intacts. | ✓ VERIFIED | `SettingsService.Load()` (l. 92-130) travaille valeur par valeur. `ValeurAcceptable` retire seulement le membre fautif, y compris un entier hors enum (`Enum.IsDefined`) et un `null` interdit. La lecture finale redonne l'initialiseur de la propriété. Les tests `Fixture_valeurs_inconnues_ne_coute_que_les_valeurs_fautives`, `Save_apres_retombees_reecrit_le_fichier_sans_perte`, `Corner_invalide_retombe_sur_TopRight…`, `Chaque_enum_des_reglages_retombe_sur_son_propre_defaut`, `Tuiles_ne_coute_jamais_les_autres_reglages` et `Cle_de_theme_inconnue_est_conservee…` couvrent le critère. |
| 2 | Un JSON illisible reste un cas à part, sans réglages à moitié lus. | ✓ VERIFIED | Le document entier est parsé avant toute exploitation. Une racine non objet, une erreur de syntaxe ou d'E/S donne `Illisible` et `new ChronosSettings()`. Aucune exception n'est levée. Test : `Json_illisible_redonne_les_defauts_entiers_jamais_a_moitie_lu`. |
| 3 | `--statusline` et tout `--xxx` inconnu sortent en silence, code 0, avant le verrou. | ✓ VERIFIED | `ArgumentsDemarrage.Trier` est un tri pur en liste blanche (`ModesConnus`). Dans `App.xaml.cs`, `OnStartup` commence par `var invocation = ArgumentsDemarrage.Trier(e.Args);`. Le cas `ArgumentInconnu` appelle `Environment.Exit(0)` sans écrire sur stderr. Le verrou, le Host et `Reconcile` viennent après. Deux gardes textuelles dans `GardesPerimetreTests` (l. 527-608) prouvent l'ordre : le tri est la première instruction, il précède `base.OnStartup`, `Acquerir`, `Host.CreateApplicationBuilder()` et `.Reconcile(`. `ArgumentsDemarrageTests` couvre la table de vérité. |
| 4 | Rien d'autre ne bouge : `--hook`, le mode CLI et les galeries restent comme en 3.4.0, avec 0 échec et 0 warning. | ✓ VERIFIED | La préséance du tri est identique à la 3.4.0 : `--statusline` > `--hook` > `--cadrans` > `--sessions` > `--historique`. Le mode est cherché partout et sans tenir compte de la casse. Les arguments sans `--` donnent l'overlay. Les cas Hook et Galeries sont conservés dans `App.xaml.cs`. Tests existants verts selon la porte de vague (1756/1756, 0 avertissement). |

**Score :** 4/4

## Artefacts requis

| Artefact | Statut | Détails |
|----------|--------|---------|
| `src/Chronos/Services/LectureReglages.cs` | ✓ VERIFIED | Contient `IssueLectureReglages` et le record `LectureReglages`. Utilisé par `SettingsService` (`DerniereLecture`, `DernieresRetombees`). |
| `src/Chronos/Services/SettingsService.cs` | ✓ VERIFIED | `Load` tolérant, `Save` atomique inchangé. |
| `src/Chronos/Services/DiagnosticService.cs` | ✓ VERIFIED | Ligne « Réglages (settings.json) : » (l. 450) alimentée par `LibelleLectureReglages`. |
| `src/Chronos/Services/ArgumentsDemarrage.cs` | ✓ VERIFIED | `ModeDemarrage`, `InvocationDemarrage`, `ArgumentsDemarrage.Trier` et `ModesConnus`. Appelé une seule fois, dans `OnStartup`. |
| `src/Chronos/App.xaml.cs` | ✓ VERIFIED | `OnStartup` aiguillé par `switch (invocation.Mode)`. |
| `tests/Chronos.Tests/ArgumentsDemarrageTests.cs` | ✓ VERIFIED | Table de vérité, preuve de la liste blanche (`ArgumentInconnu`). |
| `tests/Chronos.Tests/SettingsServiceTests.cs` | ✓ VERIFIED | Une vingtaine de tests, dont les pièges `SessionStyle`, `ReglagesSection` et `Corner`. |
| `tests/Chronos.Tests/GardesPerimetreTests.cs` | ✓ VERIFIED | Gardes d'ordre pour `App.xaml.cs`. |

## Liens clés

| De | Vers | Via | Statut |
|----|------|-----|--------|
| `App.OnStartup` | `ArgumentsDemarrage.Trier` | Première instruction, avant verrou, Host et hooks | ✓ WIRED |
| `ArgumentInconnu` | Sortie | `Environment.Exit(0)` avant `VerrouInstanceUnique.Acquerir` | ✓ WIRED |
| `SettingsService.Load` | `DerniereLecture` | Renseignée à chaque issue | ✓ WIRED |
| `DerniereLecture` | `DiagnosticService` | Ligne « Réglages (settings.json) : » dans le rapport (donc dans `chronos.log`) | ✓ WIRED |

## Couverture des exigences

| Exigence | Plan source | Statut | Preuve |
|----------|-------------|--------|--------|
| SOC-01 | 36-01-PLAN (`requirements: [SOC-01]`) | ✓ SATISFAITE | Critères 1 et 2 ci-dessus. Cochée dans REQUIREMENTS.md. |
| SOC-02 | 36-02-PLAN (`requirements: [SOC-02]`) | ✓ SATISFAITE | Critères 3 et 4 ci-dessus. Cochée dans REQUIREMENTS.md. |

Aucune exigence orpheline. REQUIREMENTS.md n'associe que SOC-01 et SOC-02 à la phase 36, et les deux sont déclarées par un plan.

## Anti-patterns

Aucun bloquant. Les `return` précoces de `OnStartup` sont voulus. `Environment.Exit(0)` suivi de `return` est le motif déjà utilisé par les modes `--hook` et `--statusline`.

## Vérifications comportementales

Étape 7b non rejouée : la porte de vague (1756/1756 tests, 0 avertissement) est fournie par l'orchestrateur, et lancer l'exe est interdit.

## Vérification humaine

Le smoke manuel de l'exe réel avec `--zzz` est explicitement reporté à la phase 43 (36-VALIDATION.md, Manual-Only). Il n'est pas compté comme un écart de la phase 36.

## Synthèse

Aucun écart. Les deux pièges identifiés par l'inventaire sont réglés dans le code et épinglés par des tests. La lecture des réglages est tolérante valeur par valeur. Les arguments sont triés avant le verrou, en liste blanche : retirer `--statusline` en phase 37 le fera tomber de lui-même dans `ArgumentInconnu`. La garde d'ordre tolère déjà l'absence de `case ModeDemarrage.StatusLine`.

Une nuance, sans écart : le membre `Tuiles` existe encore dans le code (sa suppression est en phase 38). Les tests épinglent donc la valeur `"Tuiles"` via des chaînes et des enums par réflexion, ce qui reste valable après la suppression.

---

_Vérifié : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
