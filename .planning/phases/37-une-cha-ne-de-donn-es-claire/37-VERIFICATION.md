---
phase: 37-une-cha-ne-de-donn-es-claire
verified: 2026-10-03T00:00:00Z
status: passed
score: 4/4 critères de succès vérifiés (5/5 exigences)
---

# Phase 37 : Une chaîne de données claire — rapport de vérification

**Objectif :** purge R5 selon la liste validée, chaîne réduite à `LastExact(Journal(Composite(sonde, OAuth Chronos)))`, barre statusLine retirée avec sauvegarde, diagnostic « Chaîne de données », méthodologie unique, un commit réversible par étape.
**Re-vérification :** non (vérification initiale).
**Périmètre :** vérification par lecture du code et de l'historique git. L'exe n'a pas été lancé et le vrai `~/.claude/settings.json` n'a pas été touché. Les constats sur l'exe réel (barre disparue, `--statusline` silencieux, durée du diagnostic) sont reportés à la phase 43 (37-VALIDATION.md, Manual-Only) et ne comptent pas comme écarts. La porte de phase (build `-warnaserror` à 0 avertissement, 1704/1704 tests) est reprise telle que fournie, sans relance.

## Vérité observable par critère

| # | Critère | Statut | Preuve |
|---|---------|--------|--------|
| 1 | Sources mortes disparues, un composite, commits séparés | VERIFIE | Voir détail ci-dessous |
| 2 | Barre retirée proprement (sauvegarde, idempotent, tiers intact, `--statusline` silencieux) | VERIFIE | `ClaudeSettingsReconciler`, `ArgumentsDemarrage`, tests |
| 3 | Diagnostic « Chaîne de données », sans sections mortes ni recherche de coffres | VERIFIE | `DiagnosticService.cs` |
| 4 | Méthodologie unique écrite et gardée | VERIFIE | `docs/data-sources.md`, README, CLAUDE.md, garde documentaire |

### Critère 1 : sources mortes
- Recherche de tous les termes de la liste dans `src/` : `ClaudeOAuthUsageProvider`, `GatedOAuthUsageProvider`, `ClaudeTokenReader`, `WindowsCredentialStore`, `InventaireMachine`, `ClaudeUsageObjectProvider`, `FiveHourWindowInference`, `WeeklyWindow`, `WeeklyRecalibration`, `OAuthUsageEnabled`, `EndpointOAuthClaude`, `PontStatusLine`, `StatusLineBridge`, `StatusLineInstaller`, `InnerStatusLineCommand`, `StatusLinePromptDismissed` : **aucune occurrence dans `src/`**. Les seules occurrences sont dans `tests/`, à titre de gardes de non-retour, de tests de tolérance (valeurs de journal retirées ignorées sans exception, `OAuthUsageEnabled` ignoré à la lecture des réglages) et d'un test de la migration `PurgerPrefixe` qui porte sur `ArchiveStore`. Aucune occurrence dans README, docs ni CLAUDE.md hors garde.
- Composition racine (`App.xaml.cs` l. 418-426) : un seul `CompositeUsageProvider(primary: RateLimitHeaderUsageProvider, fallback: ChronosOAuthUsageProvider)` sous `JournalisationUsageProvider`, puis `LastExactUsageProvider` (l. 447-451). La chaîne est conforme à la cible.
- Commits de purge distincts, un par étape et dans l'ordre contractuel : `6dcda8f` (étape 1, diagnostic et docs), `d6912ab` (2, orphelins), `7d48cfb` (3, jeton de l'app bureau), `902c714` (4, recalibrage), `6a655a3` (5, pont statusLine et retrait de la barre). Chacun est suivi de son commit `docs(...)`, ce qui les rend réversibles seuls.
- Journal existant relisible : tests `LigneJournalTests` (sources `EndpointOAuthClaude` et `PontStatusLine` ignorées sans exception). Ancre `WeeklyAnchor` : conservée en lecture secours par l'Historique (SUMMARY 37-04 et doc §3).
- Garde « 3 composites » devenue « 1 composite » : commentaire et miroir de chaîne unique dans `CompositionRootTests` (l. 369, 418). `GardesDoctrineTests` mentionne encore « trois composites » dans un message d'assertion historique (voir Remarques).

### Critère 2 : retrait de la barre
- `ClaudeSettingsReconciler.RetirerBarreChronos` : `Absente` si pas de clé, `Tierce` (intouchée) si la forme est inconnue ou n'appartient pas à Chronos, `Retiree` sinon, avec restauration de la barre d'origine quand elle est connue.
- `Reconcile` : sauvegarde AVANT écriture, abandon si la sauvegarde échoue, écriture atomique, rien d'écrit si le fichier est déjà conforme (idempotence) ou illisible, fichier jamais créé, `DernierBilan` renseigné. Rétention de 5 sauvegardes.
- Appelé dans `App.xaml.cs` en mode overlay uniquement, après `Show`, avant le journal de démarrage qui écrit le bilan dans `chronos.log` (section « Réglages de Claude Code », `DiagnosticService` l. 509). Les hooks sont repointés dans la même écriture.
- Tests (`ClaudeSettingsReconcilerTests`) : barre Chronos retirée, barre d'une autre version, barre tierce intacte, sans `statusLine`, fichier illisible inchangé octet pour octet, fichier absent, point fixe, une seule sauvegarde puis plus rien, hooks dans la même écriture, test interdisant de cibler le vrai settings du profil.
- `ArgumentsDemarrage` : liste blanche (`--hook`, `--cadrans`, `--sessions`, `--historique`) ; `--statusline` tombe dans `ArgumentInconnu` et sort en silence (garde phase 36). Fichiers `StatusLineBridge`, `StatusLineInstaller`, `IStatusLineSetup`, `StatusLineSetup`, `ClaudeUsageObjectProvider` supprimés ; carte réglages retirée (`ReglagesWindow.xaml`, -19 lignes).

### Critère 3 : diagnostic
- Section `[Chaîne de données]` (`DiagnosticService` l. 172) : chaîne dans l'ordre réel, sonde (interrupteur, cadence, dernier résultat, noms d'en-têtes, dépassement), secours OAuth, dernier exact et journal.
- Absence de « pont statusLine », « Usage exact (OAuth) » et « Conseil » dans le service. L'en-tête de classe précise « aucun appel réseau, aucune recherche de coffres ». Le rapport journalise « Rapport construit en N ms » pour la mesure de phase 43.

### Critère 4 : méthodologie
- `docs/data-sources.md` (461 lignes) : §1 chaîne source → cadran, priorité de la sonde, §2 transcripts (activité et plancher, jamais un pourcentage), §3 exact ou non (« Seul le plancher « ≥ » n'est pas exact »), §5 sources retirées en 3.5.
- README « D'où viennent les chiffres » réécrit sur la chaîne unique ; CLAUDE.md aligné (secours, tête, plancher seul non exact).
- `GardeDocumentationChaineTests` : liste de termes interdits (classes retirées, « repli JSONL », « Estimation (repli) », `usage.json`, `--statusline`…), avec contrôle anti-muet (fichier présent, non vide, plus de 300 lignes, chemin injecté par le csproj de tests).
- Commentaires « repli JSONL / estimation » : aucun résidu dans `CompositeUsageProvider.cs` ni `WindowState.cs` (la mention d'« estimation ABSOLUE » dans `WindowState` est une mise en garde, non une source). `SourceReliability.cs` n'existe plus sous ce nom.

## Rien de supprimé hors liste ; liste « gardés » intacte

Fichiers supprimés par les cinq commits de purge, comparés à la liste : tous relèvent d'une ligne de la liste.
- Étape 2 : `FiveHourWindowInference`, `WeeklyWindow` (+ tests).
- Étape 3 : `ClaudeOAuthUsageProvider`, `GatedOAuthUsageProvider`, `ClaudeTokenReader` (+ interface), `WindowsCredentialStore`, `InventaireMachine` (+ interface), fakes et coffre de test associés.
- Étape 4 : `WeeklyRecalibration`, `IRecalibrationPrompt`, `RecalibrationViewModel`, `RecalibrationDialog` (xaml et code), `RecalibrationPrompt`, fake et tests.
- Étape 5 : `ClaudeUsageObjectProvider`, `StatusLineBridge`, `StatusLineInstaller`, `IStatusLineSetup`, `StatusLineSetup`, fakes, tests et jeux `usage-*.json`.
- Aucun autre fichier supprimé. Les étapes 1 et 2 n'ont pas supprimé d'autres fichiers que ceux nommés.

Les 12 éléments gardés existent tous comme types dans `src/` : `RateLimitHeaderUsageProvider`, `ChronosOAuthUsageProvider`, `ChronosTokenAuthority`, `TokenRefreshService`, `OAuthLogin`, `LastExactUsageProvider`, `LastExactStore`, `DoctrineFraicheur`, `TranscriptActivityProvider`, `SourceActiviteMemoisee`, `DedupUsage`, `JournalisationUsageProvider`. Tous sont câblés dans `App.xaml.cs` ou la chaîne.

## Couverture des exigences

| Exigence | Plans | Statut | Preuve |
|----------|-------|--------|--------|
| DAT-01 (liste validée avant suppression) | 37-01 | SATISFAITE | `liste-purge.md` validée avant exécution, cochée dans REQUIREMENTS.md |
| DAT-02 (sources mortes disparues) | 37-02 à 37-06 | SATISFAITE | Critère 1 |
| DAT-03 (barre retirée avec sauvegarde) | 37-05, 37-06 | SATISFAITE | Critère 2 |
| DAT-04 (diagnostic « Chaîne de données ») | 37-01 | SATISFAITE | Critère 3 |
| DAT-05 (méthodologie unique) | 37-01 | SATISFAITE | Critère 4 |

Aucune exigence orpheline : les cinq identifiants de la phase dans REQUIREMENTS.md (DAT-01 à DAT-05, tous « Complete ») figurent dans le champ `requirements` d'au moins un plan.

## Vérifications comportementales
Étape 7b : non relancée. Les tests (1704/1704) et le build à 0 avertissement sont ceux de la porte de phase fournie.

## Remarques (non bloquantes)
- Le message d'assertion de `GardesDoctrineTests` (l. 56-58) parle encore de « trois composites » alors que la chaîne n'en a plus qu'un. C'est un texte de diagnostic de test, sans effet sur le comportement ni sur la garde documentaire.
- L'arbre de travail contient des modifications non commitées issues de la phase 38 (Historique : `PisteNiveau`, `DesignTokens`, suppressions de `PisteFenetres5h` et `Tuiles5h`). Elles sont hors périmètre de la phase 37 et n'ont pas été comptées.

## Vérification humaine (déjà planifiée en phase 43)
Barre disparue de Claude Code après un vrai premier lancement, `--statusline` silencieux sur l'exe publié, durée réelle du diagnostic (« Rapport construit en N ms »).

## Résumé
Aucun écart. Le code correspond à l'objectif : sources mortes absentes de `src/`, chaîne à un composite, retrait de barre idempotent et testé sur fichiers témoins, diagnostic et documentation alignés et gardés, cinq commits réversibles dans l'ordre contractuel, aucune suppression hors liste.

---

_Vérifié : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
