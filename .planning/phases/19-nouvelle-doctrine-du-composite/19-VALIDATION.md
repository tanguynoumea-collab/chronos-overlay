---
phase: 19
slug: nouvelle-doctrine-du-composite
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-12
---

# Phase 19 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~LastExact\|FullyQualifiedName~Composite\|FullyQualifiedName~Doctrine"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | **~2 min 15** · baseline mesurée : **652 tests / 0 échec** |

Note : la durée a triplé en phase 18 (56 s → ~2 min 15) parce que chaque test de `DiagnosticServiceTests`
exécute `BuildReportAsync`, qui balaie `%APPDATA%` / `%LOCALAPPDATA%` et fait un **poll UIA réel**. Dette
consignée, candidate phase 20 — ne pas la traiter ici.

## Sampling Rate

- Après chaque commit de tâche : commande rapide
- Après chaque vague : suite complète
- Avant vérification de phase : suite complète verte

## Tâche zéro recommandée par la recherche

Avant toute conception : **ajouter `CapturedAt` aux 3 providers qui ne le posent pas**
(`ChronosOAuthUsageProvider`, `ClaudeOAuthUsageProvider`, `ClaudeUsageObjectProvider` — seule la sonde
d'en-têtes le renseigne aujourd'hui), puis lancer la suite et **prendre la liste d'échecs comme inventaire
réel** des ~120 tests de providers non lus exhaustivement. 2 min 15 contre une journée d'hypothèses.

## Le piège le plus grave de la phase

**Horodater `ClaudeUsageObjectProvider` avec `now` au lieu du `capturedAt` du fichier ressusciterait le bug
des « 10 % »**, cette fois avec l'autorité d'une limite d'âge prétendument appliquée. Un test doit
explicitement charger un `usage.json` ancien et vérifier que son âge est celui du FICHIER, pas de la lecture.

## Contraintes de sécurité

- **Aucune requête réseau réelle depuis un test.**
- Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL de `%APPDATA%\Chronos\oauth.dat`.
  Contrôle avant/après chaque plan : **518 octets, mtime 1783863147**.
- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` — chemins depuis `Path.GetTempPath()`.
- Les fichiers SOURCE contenant `RefreshAsync` doivent rester exactement 2 (exclure `bin/` et `obj/`).

## Per-Task Verification Map

*Rempli par le planner.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| — | — | — | — | — | — | — | ⬜ pending |

## Les quatre branches de la doctrine — chacune doit avoir un test qui tombe si la branche disparaît

| Branche | Ce qui doit être vrai | Exigence |
|---------|----------------------|----------|
| 1. Exact frais | Relevé sous la limite d'âge : affiché tel quel | EXA-02 |
| 2. Encore rigoureusement valide | Aucune réponse assistant depuis l'horodatage du relevé → **encore exact**, pas périmé | DEL-03 |
| 3. Plancher marqué | Activité depuis → « **≥ N %** », incertitude **unilatérale**, `Utilization` **jamais gonflée** | DEL-04 |
| 4. Indisponible | Sinon. Et si aucun exact n'a **jamais** été obtenu : invitation à se connecter, **jamais un pourcentage** | EXA-05 |

**DEL-04 n'a pas de réponse numérique honnête et n'en a pas besoin** : le delta change la NATURE du chiffre,
il ne s'y ajoute pas. Mesure à l'appui : 643 649 933 tokens sur 5 h contre l'ancien plafond de 230 000 000
= 280 %. Conséquence imposée à la phase 20 : **pas d'arc de delta, pas de barre d'erreur.**

**EXA-04 est une interdiction, pas une fonctionnalité** : aucune utilization absolue dérivée d'un comptage de
tokens ne doit réapparaître. `tokens / plafond` a été supprimé en phase 16 — une garde de balayage du texte
source doit le maintenir mort.

## Gardes permanentes

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté Services | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert |
| Composition DI | `--filter "FullyQualifiedName~CompositionRootTests"` | vert |
| Position de la sonde | `--filter "La_sonde_d_en_tetes_est_le_PRIMAIRE"` | vert — garde falsifiable de la phase 18, **ne pas la casser silencieusement** |
| Normalisation unique | `--filter "FullyQualifiedName~NormalisationUniqueTests"` | vert — aucune conversion divergente réintroduite |
| Coffre intact | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec |

Toutes les commandes `--filter` se préfixent de `dotnet test Chronos.sln -v q --nologo`.

## Wave 0 Requirements

- [ ] `CapturedAt` posé par les 3 providers qui l'omettent, puis inventaire des échecs réels
- [ ] Horloge injectée (`FakeClock` existe) sur tout chemin décisionnel : les 4 branches doivent être
      testables de façon **déterministe**
- [ ] Fixture `usage.json` ancienne, pour prouver que l'âge retenu est celui du FICHIER
- [ ] Fixtures de transcripts : activité depuis T / aucune activité depuis T / T antérieur à l'horizon
      (`Covers(T) == false`, où le delta ne peut pas être garanti)

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Le chiffre affiché est le bon après deux mois de panne | EXA-02 | Dépend de l'état réel de la machine | Après reconnexion, comparer le chiffre du cadran avec `/usage` dans Claude Code |

## Validation Sign-Off

- [ ] Les 4 branches ont chacune un test qui tombe si la branche est supprimée
- [ ] Aucun pourcentage n'est jamais inventé, dans aucune branche
- [ ] `Utilization` n'est jamais gonflée par un delta
- [ ] La garde de position de la sonde (phase 18) est toujours verte
- [ ] `tokens / plafond` reste mort (garde de balayage du texte source)
