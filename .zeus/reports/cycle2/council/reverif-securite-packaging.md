# Re-vérification ciblée Sécurité / Packaging / Mainteneur — phase 42.2 (2026-10-04)

> Rapport rendu par l'agent (sans outil d'écriture), reporté par l'orchestrateur, condensé. HEAD d772e9a.

Phase 1 : 0 paquet vulnérable ; 296/296 tests ciblés verts ; mode développeur absent (lien symbolique non testable) ;
aucune ressource XAML orpheline ; Security Code Scan non vérifié (absent).

| ID | Verdict | Preuve |
|---|---|---|
| SEC-2 | FERMÉ | `SessionHookProcessor.cs:62-63,99`, `EcritureEtatSession.cs:65-73`, tests `EcritureEtatSessionTests.cs:459-531` |
| PKG-1 | FERMÉ (code) | `AutostartService.cs:38, 99-112`, `App.xaml.cs:120-126` |
| PKG-7 | PARTIELLEMENT | `scripts/` fermé ; reliquat doc `README.md:199,206`, `Chronos.csproj:15` → phase 43 |
| PERT-1 | FERMÉ | pont/installeur/fixture retirés, garde sans *.js/*.mjs |
| MAINT-5 | FERMÉ | `DiagnosticService.cs:352` boucle sur `SessionHookInstaller.Events` (8) |
| PERT-2 / PERT-3 / PERT-4 / PERT-5 / MAINT-11 | FERMÉS | 0 occurrence des symboles retirés ; `RateLimitHeaderUsageProvider.cs:239-251` |

Passerelle `~/.claude/settings.json` : conforme (voie unique, inexploitable → rien écrit, sauvegarde avant écriture,
refus si modifié/apparu, refus lien symbolique à la lecture du code, écriture atomique, ne crée jamais le fichier).
Quarantaine : conforme (renommage seul, même dossier, jamais de suppression, pas sur E/S passagère).

## Nouveaux findings
- **SEC-R1 — Mineur** — `SessionHookProcessor.cs:62-63`, `EcritureEtatSession.cs:205` : `$` accepte un `\n` final (`"abc\n"` passe) ; pas de sortie du dossier, écart doc/comportement et ~50-100 ms de reprises. Reco : `\z` + test.
- **PKG-R1 — Mineur** — `AutostartService.cs:99-112`, `App.xaml.cs:120` : « dernier lancé gagne » — un build F5 (`bin\Debug`), une copie dans Téléchargements/%TEMP% ou une version plus ancienne reprend l'autostart en silence. Reco : ne repointer que si la cible est absente ou de version inférieure ; exclure `bin\` et `%TEMP%`.
- **DIAG-R1 — Mineur** — `DiagnosticService.cs:345, 361` : filtre `cmd.Contains("--hook")` au lieu de `ClaudeSettingsJson.IsChronosCommand`, et parse sans `CommentHandling.Skip` → hooks tiers comptés (PreToolUse/PostToolUse), settings commenté = « lecture impossible ». Reco : `IsChronosCommand` + options tolérantes.
- **PKG-R2 — Info** — `docs/publish.md:95-97,142-144` et diagnostic : convergence de l'autostart non documentée, cible non affichée → phase 43.
- **SEC-R3 — Info** — `PasserelleReglagesClaudeTests.cs:195-196` : le test du lien symbolique fait `return` quand la création échoue → compté réussi sans rien vérifier. Reco : saut explicite.
- **SEC-R4 — Info** — `PasserelleReglagesClaude.cs:186-208` : 5 copies en clair (même ACL), tri culture-dépendant, état d'origine perdu après 5 écritures (préexistant). Reco : tri ordinal UTC, garder la première sauvegarde.

Comptes : 9 FERMÉS, 1 PARTIEL, 0 OUVERT ; nouveaux : 0 B, 0 Maj, 3 Min, 3 Info.
