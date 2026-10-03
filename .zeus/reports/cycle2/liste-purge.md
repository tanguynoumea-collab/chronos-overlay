# Liste de purge R5 — à valider par l'utilisateur AVANT toute suppression (exigence DAT-01)

Source : `.zeus/reports/cycle2/inventaire-purge-donnees.md`. Preuve terrain : depuis septembre, **1 531 relevés de la sonde
d'en-têtes**, 7 du secours OAuth Chronos, **zéro** du jeton de l'app bureau, **zéro** du pont statusLine.

## Ce qui reste (la chaîne claire)

```
Sonde d'en-têtes (1 requête / 5 min)  ─┐
                                        ├─ Composite (par fenêtre, sonde prioritaire) ─ Journal ─ Dernier exact ─ Cadran
Secours OAuth du login Chronos ────────┘
```
- **Exact** : sonde, secours OAuth, dernier relevé frais (≤ 6 min) ou « encore valide » (aucune activité Claude Code depuis).
- **Pas exact** : seulement le plancher « ≥ X % » (dernier relevé vieilli alors que Claude Code a travaillé). Rien d'autre.
- Gardés : `RateLimitHeaderUsageProvider`, `ChronosOAuthUsageProvider`, `ChronosTokenAuthority`, `TokenRefreshService`,
  `OAuthLogin`, `LastExactUsageProvider`, `LastExactStore`, `DoctrineFraicheur`, `TranscriptActivityProvider`,
  `SourceActiviteMemoisee`, `DedupUsage`, `JournalisationUsageProvider`.

## Ce qui part

| # | Élément | Pourquoi | Ce que tu verras |
|---|---|---|---|
| 1 | Lecture du jeton de l'app bureau : `ClaudeOAuthUsageProvider`, `GatedOAuthUsageProvider`, `ClaudeTokenReader` (déchiffrement du coffre), `WindowsCredentialStore`, `InventaireMachine` | n'a jamais produit un relevé (le coffre de l'app bureau est dans le paquet MSIX) ; c'est aussi le code le plus sensible (il déchiffre le jeton d'une autre application) | rien |
| 2 | Pont statusLine : mode `--statusline`, `StatusLineBridge`, `StatusLineInstaller`, proposition au premier lancement, `ClaudeUsageObjectProvider`, lecture et surveillance de `usage.json` | décision utilisateur ; `usage.json` figé depuis le 10/07 | **la barre en bas de Claude Code disparaît** (Chronos la retire de `~/.claude/settings.json` après une sauvegarde) ; carte « Barre de statut de Claude Code » retirée des réglages |
| 3 | Recalibrage hebdo : `WeeklyRecalibration`, dialogue « Recalibrer… », sa carte dans les réglages | ne sert que si le serveur ne donne pas le reset hebdo, ce qui n'arrive plus avec la sonde | carte « Recalibrer… » retirée ; l'ancre hebdo déjà enregistrée reste lue en secours par l'Historique |
| 4 | Orphelins : `FiveHourWindowInference`, `WeeklyWindow`, réglage `OAuthUsageEnabled` et sa commande | plus aucun appelant en production | rien |
| 5 | Diagnostic : sections « pont statusLine », « endpoint OAuth (repli) » (coffre, cartographie des dossiers, appel HTTP), « Réglage Usage exact (OAuth) », « Conseil » | sources mortes ; le « Conseil » recommande encore le pont statusLine ; la recherche des coffres coûte ≈ 17 s par rapport | diagnostic plus court et plus rapide, avec une nouvelle section « Chaîne de données » |
| 6 | Valeurs `EndpointOAuthClaude` et `PontStatusLine` des sources du journal | plus aucun producteur ; aucune ligne du journal ne les contient | rien (le journal reste lisible) |
| 7 | Migration ponctuelle `PurgerPrefixe("desktop:")` (ancienne source UIA) | ne fait plus rien (`archived.json` vide) | rien |
| 8 | Docs et commentaires périmés : README « D'où viennent les chiffres » (entièrement faux), `docs/data-sources.md` §1-5, `CLAUDE.md`, commentaires « repli JSONL / estimation » | décrivent des méthodes abandonnées | docs réécrites autour de la chaîne ci-dessus |

Les tests de chaque élément supprimé partent avec lui. Les gardes de doctrine (« exact ou rien ») restent et doivent rester vertes
à chaque commit ; la garde « 3 composites » passe à « 1 composite ».

## Ordre d'exécution (un commit par ligne, réversible)
1. Diagnostic et docs (§5, §8) · 2. Orphelins (§4, §7) · 3. Jeton de l'app bureau (§1, §6) · 4. Recalibrage (§3) ·
5. Pont statusLine + retrait de la barre (§2), en dernier, derrière la garde d'arguments inconnus (phase 36).
