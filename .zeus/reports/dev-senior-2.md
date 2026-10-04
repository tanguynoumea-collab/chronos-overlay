# Re-vérification dev-senior n° 2 — Chronos, phase 42.3 (2026-10-04)

> Rapport rendu par l'agent en lecture seule (sans écriture), reporté par l'orchestrateur ZEUS, condensé.

Mesures : `dotnet build Chronos.sln -warnaserror --no-incremental` 0 avertissement ; `dotnet test Chronos.sln` 2 179 réussis,
0 échec, 1 ignoré (lien symbolique) ; tests ciblés de la phase 127/127.

## Verdicts sur dev-senior-1

| Finding | Verdict | Preuve |
|---|---|---|
| DS-ARCH-01 / P-01 déconnexion | FERMÉ (reliquat DS2-01) | `MainViewModel.cs:849-853`, `ChronosTokenAuthority.cs:115-144`, 5+3 tests |
| DS-ARCH-02 / P-02 « encore valide » | PARTIEL | hypothèse écrite `AUDIT_POINTS.md:9-13`, `docs/data-sources.md` §4 ; journal périmé non traité (DS2-02) |
| DS-ARCH-03 / P-03 diagnostic rappelle la chaîne | FERMÉ | `App.xaml.cs:562`, `RefreshOrchestrator.cs:80-81`, `DiagnosticService.cs:159`, test 0 GetAsync |
| DS-PERF-03 démarrage sur thread UI | FERMÉ | `App.xaml.cs:176-177`, `ConfigureAwait(false)` |
| DS-ARCH-04 / P-04 détecteur | FERMÉ | `SessionTreatmentTracker.cs:43,72,127`, `SessionMonitor.cs:214`, `DiagnosticService.cs:475` |
| DS-PERF-01 / P-07 ét. 1 transcripts | FERMÉ | `TranscriptActivityProvider.cs:133-168`, tests A-F |
| DS-MAINT-04 reliquats | FERMÉ (périmètre retenu) | `PercentFormatter`, `SettingsService.cs:252` |
| DS-MAINT-03 commentaires | PARTIEL (attendu) | reste `docs/publish.md` §5, `docs/data-sources.md:120` |
| DS-MAINT-01 / P-09 version | OUVERT → phase 43 | `Chronos.csproj:16-19` en 3.4.0 |
| Non retenus | ouverts | DS-PERF-02, DS-ARCH-05, DS-DUP-01..04, DS-MAINT-06 |

## Nouveaux findings

- **DS2-01 — Mineur (conséquence large)** — `ChronosTokenAuthority.cs:94-160,190-200` : `ReinitialiserApresLogin` hors verrou,
  `_jetons` relu sans copie locale → NullReferenceException possible dans `GetAccessTokenAsync`, non rattrapée
  (`RateLimitHeaderUsageProvider.cs:224`, `CompositeUsageProvider.cs:39-42`, `LastExactUsageProvider.cs:62`) → la boucle de
  l'orchestrateur (`RefreshOrchestrator.cs:85`, ne rattrape que l'annulation) meurt en silence ; et fenêtre de quelques ms entre
  vérification de génération (`:120`) et `Save` (`:152`) → coffre recréé après déconnexion. Proposition : copie locale, re-vérif
  de génération autour de `Save` (effacer si changée), `catch (Exception)` journalisé dans `BoucleAsync`, test à barrière.
- **DS2-02 — Majeur (probabilité faible)** — `SourceActiviteMemoisee.cs:55-59` rend l'ancien journal quel que soit son âge ;
  `DoctrineFraicheur.cs:78-83` ne vérifie jamais `journal.Now` → si la relecture échoue, un relevé peut rester « exact » alors que
  Claude Code a travaillé. Hors de l'hypothèse écrite. Proposition : `catch when (_journal is not null && EncoreValide(_journal, now))`,
  sinon Indisponible ; test.
- **DS2-03 — Hypothèse (décision produit)** — après « Se déconnecter », `last-exact.json` de l'ancien compte peut rester affiché
  « exact — encore valide » jusqu'au reset. Effacer à la déconnexion, ou documenter.
- **DS2-04 — Mineur** — cache ≈ 24 Mo jamais libéré ; ordre et purge sans risque ; fichiers chauds relus en entier (étape 2 de P-07
  non faite).
- **DS2-05 — Mineur** — gardes de câblage textuelles ; compléter par un test partant du conteneur réel.

## Points d'AUDIT_POINTS

| # | Verdict |
|---|---|
| 1 Honnêteté | Sous réserve (DS2-02, DS-ARCH-05 à arbitrer) |
| 2 Intégrité | Conforme, écart mineur (DS2-01) |
| 3 Robustesse | Écarts mineurs (DS-PERF-02 non retenu, DS2-01) |
| 4 Reprenabilité | Écarts mineurs |
| 5 Prêt à distribuer | BLOQUANT jusqu'à phase 43 (version, docs/publish.md §5) |

**Verdict global : publiable après phase 43, sous réserve** — fortement conseillé d'ajouter DS2-01 et DS2-02 (quelques lignes chacun).
