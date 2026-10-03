# Phase 37 : Une chaîne de données claire — Research

**Researched:** 2026-10-03
**Domain:** purge de code C#/.NET 8 WPF (DI, MVVM, gardes textuelles et par réflexion), migration de `~/.claude/settings.json`, réécriture documentaire gardée par tests
**Confidence:** HIGH (tout est vérifié dans le code du dépôt ; aucune bibliothèque nouvelle)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Chaîne cible : `LastExactUsageProvider( JournalisationUsageProvider( CompositeUsageProvider(RateLimitHeaderUsageProvider,
  ChronosOAuthUsageProvider) ) )` — un seul composite (la garde `GardesPerimetreTests:627` passe de 3 à 1).
- Liste et ordre EXACTS : ceux de `liste-purge.md` (8 éléments, 5 étapes) ; **un commit réversible par étape**, gardes de
  doctrine vertes après chaque commit. Rien hors de cette liste n'est supprimé (la liste « à ne pas purger » reste).
- Barre statusLine (décision utilisateur : RETIRÉE). Au premier lancement de la 3.5, la réconciliation (`ClaudeSettingsReconciler`)
  **sauvegarde** `~/.claude/settings.json` puis **retire la clé `statusLine` si et seulement si elle pointe sur un exe Chronos
  `--statusline`** ; une barre d'un tiers reste intacte ; idempotent, journalisé, testé sur fichiers témoins. Les hooks restent
  réconciliés (repointés vers l'exe courant). `InnerStatusLineCommand` vaut `null` chez l'utilisateur : rien à restaurer ; si
  une valeur non nulle existait, la restaurer à la place de la barre Chronos (cas testé). Le mode `--statusline`, le pont,
  l'installeur, la proposition au premier lancement, la carte des réglages et `usage.json` (+ surveillance) disparaissent ;
  `--statusline` tombe alors dans la garde d'arguments inconnus de la phase 36 (sortie silencieuse).
- Recalibrage : `WeeklyAnchor` reste **lu** en secours par `BornesPlage` / `HistoriqueViewModel` (aucune écriture nouvelle).
- Diagnostic : nouvelle section « Chaîne de données » (sonde d'en-têtes, secours OAuth Chronos, dernier exact persisté,
  journal) ; retrait des sections pont statusLine, endpoint OAuth (repli), réglage Usage exact (OAuth), Conseil.
- Docs : `docs/data-sources.md` réécrit autour de la chaîne (exact = sonde, secours OAuth, dernier exact frais ou encore
  valide ; non exact = seulement le plancher « ≥ ») ; README « D'où viennent les chiffres » ; `CLAUDE.md` (sections Contexte /
  Sources de données) ; commentaires « repli JSONL / estimation » corrigés. Gardes de documentation existantes vertes ou
  adaptées explicitement.
- Mémoire hors dépôt : ne PAS modifier `~/.claude/projects/.../memory` (l'orchestrateur s'en charge).

### Claude's Discretion
Découpage en plans par étape de la liste ; libellés de la nouvelle section du diagnostic.

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DAT-01 | Liste de purge validée avant le premier commit de suppression | **Déjà satisfait** (2026-10-03, `liste-purge.md`). Aucun plan ne le rejoue. |
| DAT-02 | Sources mortes supprimées ; chaîne `LastExact(Journal(Composite(sonde, OAuth Chronos)))`, gardée par test (un composite) | Inventaire fichier par fichier § « Étapes 2, 3, 4, 5 » ; trois conflits d'ordre à neutraliser (§ « Conflits d'ordre ») ; garde composite 3 → 2 → 1 ; garde de non-retour par réflexion |
| DAT-03 | Retrait de la barre statusLine au 1er lancement 3.5 (sauvegarde, idempotent, journalisé, fichiers témoins) ; pont, installeur, carte retirés ; hooks réconciliés | § « Mécanisme de retrait de la barre » (code), § « Journalisation dans chronos.log » (course `LogStartupAsync`), lecture héritée d'`InnerStatusLineCommand` avant `window.Show()` |
| DAT-04 | Diagnostic = chaîne réelle (« Chaîne de données ») ; sections mortes retirées ; plus de recherche des coffres (≈ 17 s) | § « Étape 1 — diagnostic » (lignes exactes de `DiagnosticService.cs`), piège de la variable `claudeSettings`, garde structurelle anti-coffres, mesure de durée |
| DAT-05 | Méthodologie unique écrite (data-sources, README, CLAUDE.md, commentaires) ; garde documentaire rouge si une source retirée est citée | § « Docs » : sections de `data-sources.md` épinglées par 5 gardes existantes, liste des commentaires, nouvelle garde `GardeDocumentationChaineTests` |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit.Mvvm / Microsoft.Extensions.DependencyInjection + Hosting — imposé.
- MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services.
- Services et Models **neutres** (aucun type WPF — `ServicesLayerPurityTests`) ; l'UI vit dans Views.
- Chemins uniquement sous `%USERPROFILE%` / `%APPDATA%` ; jamais `Assembly.Location` (vide en mono-fichier) → `Environment.ProcessPath`.
- Honnêteté des chiffres : ne jamais présenter une estimation comme exacte ; seul le plancher « ≥ » est non exact.
- Robustesse : aucune source ≠ crash ; parsing tolérant ; un `settings.json` inexploitable ⇒ on n'écrit RIEN.
- UI et commentaires en **français**.
- Workflow GSD : modifications via `/gsd:execute-phase`.
- Convention de phase (36-VALIDATION) : `dotnet build Chronos.sln` **0 avertissement** puis `dotnet test Chronos.sln` vert.
- Mémoire `~/.claude/projects/.../memory` : NE PAS toucher (CONTEXT).

## Summary

La purge est mécaniquement simple (suppression de ~25 fichiers source et ~12 fichiers de test) mais **dangereuse par cascade** :
trois éléments de la liste validée ne peuvent pas partir à l'étape où la liste les range sans casser la compilation, deux
constructeurs à paramètres positionnels (`DiagnosticService`, `MainViewModel`) sont construits à la main dans ~25 sites de
tests, et cinq gardes documentaires épinglent la structure numérotée de `docs/data-sources.md`. Ces conflits se résolvent
**sans rien purger de plus ni de moins** : il suffit de déplacer la suppression d'un membre à l'étape qui supprime son dernier
consommateur (détail § « Conflits d'ordre »).

Le retrait de la barre est le seul point à risque utilisateur. Le cœur existe déjà (`StatusLineInstaller.TransformForUninstall`
+ prédicat `ClaudeSettingsJson.IsChronosCommand(cmd, "--statusline")` + sauvegarde horodatée et écriture atomique de
`ClaudeSettingsReconciler`). Il faut le **déplacer** dans le réconciliateur (qui cesse de repointer la barre et la retire),
lire l'ancienne `InnerStatusLineCommand` **avant** qu'un `Save()` des réglages ne l'efface (la propriété disparaît du record),
et résoudre une course existante : `LogStartupAsync` réécrit `chronos.log` en entier (`File.WriteAllText`) et part avant la
réconciliation — une ligne ajoutée par le réconciliateur serait écrasée.

Vérifié sur la machine (lecture seule) : `~/.claude/settings.json` contient
`{"type":"command","command":"\"C:/Users/Tanguy/Documents/PROGRAMMES/DEV/PROJET OVERLAY/Chronos-v3.4.0.exe\" --statusline"}`,
sans `padding` ; `%APPDATA%\Chronos\settings.json` porte `InnerStatusLineCommand: null`, `StatusLinePromptDismissed: false`,
`OAuthUsageEnabled: true`, `WeeklyAnchor: 2026-07-11T00:00:00+02:00`.

**Primary recommendation :** cinq plans, un par ligne de la liste, chacun = un commit qui compile à 0 avertissement et garde la
suite verte ; appliquer les trois déplacements de membres (`OAuthUsageEnabled` → étape 3, `PontStatusLine` → étape 5, composite
3 → 2 → 1) ; changer la signature de `DiagnosticService` à l'étape 3 (pas à l'étape 1) ; faire de la réconciliation une fonction
qui rend un **bilan**, appelée avant `LogStartupAsync`, et dont le diagnostic écrit la ligne dans `chronos.log`.

## Standard Stack

Aucune dépendance ajoutée ni retirée. Vérifié dans `src/Chronos/Chronos.csproj` :

| Paquet | Version | Sort |
|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | inchangé |
| Microsoft.Extensions.Hosting | 8.0.1 | inchangé |
| System.Security.Cryptography.ProtectedData | 8.0.0 | **GARDER** : utilisé aussi par `ChronosOAuthStore.cs` (coffre du login Chronos), pas seulement par `ClaudeTokenReader.cs` |
| xUnit 2.9.2 / Xunit.StaFact 1.1.11 / Microsoft.NET.Test.Sdk 17.11.1 | — | inchangés (tests) |

`System.Text.Json.Nodes` (framework) reste l'outil de mutation de `~/.claude/settings.json` via `ClaudeSettingsJson.ParseOrNull` /
`Serialize` — ne rien réécrire à la main.

## Conflits d'ordre dans la liste validée (à neutraliser, pas à rediscuter)

La liste est contractuelle ; ces ajustements ne changent **pas** ce qui est purgé, seulement le commit où un membre précis
disparaît, pour que chaque commit compile seul et reste réversible. Le plan doit les annoncer comme tels.

| # | Élément de la liste | Étape prévue | Pourquoi impossible | Résolution |
|---|---|---|---|---|
| C1 | Réglage `OAuthUsageEnabled` (§4) | 2 | `GatedOAuthUsageProvider.cs:26` lit `_settings.Load().OAuthUsageEnabled` ; ce portillon ne part qu'à l'étape 3 | Étape 2 retire `IsOAuthUsageEnabled` + `ToggleOAuthUsageCommand` (MainViewModel) ; la **propriété** `ChronosSettings.OAuthUsageEnabled` part à l'étape 3 avec le portillon |
| C2 | Valeur `SourceUsage.PontStatusLine` (§6) | 3 | `ClaudeUsageObjectProvider.cs:102` la produit ; ce provider ne part qu'à l'étape 5 | Étape 3 retire `EndpointOAuthClaude` seul ; `PontStatusLine` part à l'étape 5 |
| C3 | Garde « 3 composites » → « 1 » | 3 | après l'étape 3 la chaîne vaut `Composite(sonde, Composite(OAuthChronos, ClaudeUsageObjectProvider))` = **2** | `GardesPerimetreTests:627` : 3 → **2** à l'étape 3, 2 → **1** à l'étape 5 |

Autres points d'ordre :
- **A9 `UsageNormalization.InstantDepuisEpochMillisecondes`** (cité par l'inventaire, **absent** de la liste validée) : NE PAS
  le supprimer — il sert aussi `BalayageMagasinSessions`, `LecteurAppBureau`, `SessionTreatmentTracker` et le diagnostic
  (fichiers d'état de hook, `DiagnosticService.cs:598`).
- **`ChronosPaths.UsageFile`** : GARDER la propriété (ctor positionnel `(UsageFile, ProjectsRoot)`). Elle est l'ancre de
  `SettingsFile`, `LastExactFile`, `HistoriqueDir` et du dossier `oauth.dat` / `sessions` ; la renommer toucherait des dizaines de
  sites et sort de la liste. À l'étape 5, corriger seulement son commentaire (« usage.json n'est plus écrit depuis la 3.5 ; la
  propriété ancre le dossier %APPDATA%\Chronos »). Le fichier `usage.json` résiduel chez l'utilisateur n'est **pas** supprimé
  (donnée utilisateur, hors liste).

## Architecture Patterns

### Pattern 1 : un commit = une ligne de la liste, compilable et réversible
Chaque plan se termine par : `dotnet build Chronos.sln` (0 avertissement) → `dotnet test Chronos.sln` vert → commit. Un
`git revert` d'un commit seul doit recompiler : c'est garanti si chaque commit supprime un type **et tous ses consommateurs**
(production + tests + fakes) et rien qui serve encore à un commit ultérieur (cf. C1-C3).

### Pattern 2 : garde de non-retour par réflexion, enrichie à chaque étape (DAT-02 « gardée par test »)
Précédent : `GardesPerimetreTests.Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` (phase 21). Ajouter un test
`Aucun_maillon_retire_de_la_chaine_ne_subsiste` dont la liste de noms **grandit à chaque commit** (chaque étape ajoute ses
noms ; le revert d'une étape retire ses noms avec elle) :

```csharp
// Source : motif GardesPerimetreTests.cs:26 (réflexion sur l'assembly Chronos)
var asm = typeof(Chronos.Services.IUsageProvider).Assembly;
string[] retires =
{
    // étape 2
    "FiveHourWindowInference", "WeeklyWindow",
    // étape 3
    "ClaudeOAuthUsageProvider", "GatedOAuthUsageProvider", "ClaudeTokenReader", "IClaudeTokenReader",
    "WindowsCredentialStore", "InventaireMachine", "IInventaireMachine",
    // étape 4
    "WeeklyRecalibration", "RecalibrationViewModel", "RecalibrationDialog", "RecalibrationPrompt", "IRecalibrationPrompt",
    // étape 5
    "ClaudeUsageObjectProvider", "StatusLineBridge", "StatusLineInstaller", "StatusLineSetup", "IStatusLineSetup",
};
var revenants = asm.GetTypes().Where(t => retires.Contains(t.Name)).Select(t => t.FullName).ToList();
Assert.True(revenants.Count == 0, "…" + string.Join("\n  ", revenants));
// + membres : typeof(ChronosSettings).GetProperty("OAuthUsageEnabled") is null (étape 3),
//   "InnerStatusLineCommand"/"StatusLinePromptDismissed" (étape 5) ;
//   Enum.GetNames<SourceUsage>() ne contient pas "EndpointOAuthClaude" (3) ni "PontStatusLine" (5).
```
`WindowsCredentialStore` est `internal` : `GetTypes()` le voit quand même.

### Pattern 3 : paramètre optionnel en DERNIÈRE position (protocole du dépôt)
`DiagnosticService` et `MainViewModel` documentent ce protocole (« les N sites de construction compilent sans retouche »). Pour
ajouter le bilan de réconciliation au diagnostic (étape 5), ajouter `ClaudeSettingsReconciler? reglagesClaude = null` (ou une
interface neutre) **en dernier**. Pour RETIRER un paramètre positionnel, aucun raccourci : tous les sites changent — d'où le
regroupement à l'étape où le type disparaît.

### Anti-Patterns to Avoid
- **Laisser un champ privé assigné mais jamais lu** (ex. `_tokenReader` dans `DiagnosticService` après l'étape 1) : CS0414
  potentiel → casse « 0 avertissement ». Retirer le **champ** et garder le **paramètre** (un paramètre inutilisé ne produit pas
  d'avertissement de build ; IDE0060 est un analyseur non activé ici).
- **Repartir d'un `JsonObject` vierge** sur un `~/.claude/settings.json` illisible : interdit (`ParseOrNull` → `null` = ne rien écrire).
- **Lire `InnerStatusLineCommand` APRÈS `window.Show()`** : `OverlayController.SnapToNearestCorner` (via `RestorePlacement` →
  `ReclampToValidMonitor`, ou `DpiChanged`) peut faire `Save(Load() with …)` et la clé, devenue inconnue, disparaît du fichier.
- **Ajouter une ligne à `chronos.log` depuis le réconciliateur** sans réordonner : `LogStartupAsync` (`DiagnosticService.cs:122`,
  `File.WriteAllText`) l'écraserait — il part avant la réconciliation et attend la chaîne (réseau) avant d'écrire.
- **Supprimer du texte de doc au hasard dans `data-sources.md`** : les titres `## 2.`, `## 6.`, `## 7. Journal d'historique`,
  `## 8. Agrégats de tokens`, `## 9. Lecture par la fenêtre Historique` et la dernière ligne (« §8 ») sont épinglés.

## Inventaire par étape

Chemins relatifs à la racine du dépôt. « SUPPR » = fichier supprimé ; « MODIF » = fichier édité ; « ADAPT » = test réécrit
explicitement ; « SUPPR-T » = test/fichier de test supprimé.

### Étape 1 — Diagnostic et docs (§5, §8) — DAT-04, DAT-05

**Production**
- MODIF `src/Chronos/Services/DiagnosticService.cs` :
  - retirer `[Réglage]` (l. 169-172, lit `s.OAuthUsageEnabled`) ;
  - retirer `[Source exacte — pont statusLine Claude Code]` (l. 230-278 : lecture `statusLine`, `usage.json`) ;
  - retirer `[Source exacte — endpoint OAuth (repli)]` (l. 280-427 : `config.json`/`Local State`, `.credentials.json`,
    `_machine.CoffresOAuth` (≈ 17,7 s), cartographie des dossiers, `WindowsCredentialStore.ReadClaudeEntries`,
    `ClaudeTokenReader.ParseCredentialBlob`, `_tokenReader.TryReadAccessToken`, appel HTTP `UsageUrl`) ;
  - retirer `[Conseil]` (l. 696-703) ;
  - retirer les aides devenues mortes : `UsageUrl` (l. 25), `DescribeBlobShape` (l. 1077-1097), `Pct` (l. 1100-1103), le
    commentaire l. 1073-1074 ;
  - **PIÈGE DE COMPILATION :** la variable locale `claudeSettings` est déclarée l. 232 dans la section pont, mais **réutilisée**
    l. 520 dans `[Widget sessions Claude Code]` (hooks). La redéclarer dans la section widget (ou en tête de méthode) ;
    `bridgeInstalled` ne sert qu'au Conseil (part avec lui) ; le commentaire l. 610-611 sur la variable `e` devient caduc ;
  - retirer les **champs** `_tokenReader` et `_machine` (et `_machine = machine ?? new InventaireMachine()`), **garder les
    paramètres** `tokenReader` et `machine` du constructeur jusqu'à l'étape 3 (aucun site de test ne change à l'étape 1) ;
  - ajouter la section **« [Chaîne de données] »** (voir § « Section Chaîne de données ») ; elle absorbe les deux sections
    existantes « [Source exacte — login OAuth Chronos] » (l. 174-184) et « [Source exacte — sonde d'en-têtes de rate-limit] »
    (l. 186-228) — garder leurs lignes mot pour mot (plusieurs tests les assertent : « État d'authentification : … »,
    « Dernière sonde : … », « En-têtes « unified » reconnus : … », « Dépassement : … »).
- MODIF commentaires « repli JSONL / estimation » (liste vérifiée par grep) :
  `Services/CompositeUsageProvider.cs` l. 6-9, 16, 27 (`JsonlEstimationProvider`), 47-48, 66-70 ;
  `Models/SourceReliability.cs` l. 3 (Estimated = **plancher « ≥ »**, plus « repli JSONL ») ;
  `Models/WindowState.cs` l. 7-8 ; `Controls/Cadrans/EmberRingControl.cs` l. 10-11 ; `Controls/Cadrans/FuseBar.cs` l. 11 ;
  `Controls/Cadrans/TideColumn.cs` l. 10 ; `ViewModels/MainViewModel.cs` l. 21-25 (doc du recalibrage — peut attendre l'étape 4
  où le code part) ; `Services/RateLimitHeaderUsageProvider.cs` l. 10-11 (« sans pont statusLine, sans usage.json » → peut
  rester jusqu'à l'étape 5 ou être reformulé dès maintenant sans citer le pont).
  ⚠ Ne PAS renommer `SourceReliability.Estimated` ni les `DependencyProperty` `Estimated` des contrôles : hors liste, bindings
  XAML et tests en dépendent. Seuls les commentaires changent.

**Docs**
- MODIF `docs/data-sources.md` §1-§5 réécrits (voir § « Docs ») + ligne `source` du tableau du §7 (l. 370) : à l'étape 1, ne la
  réduire que si l'on accepte que la doc devance le code jusqu'à l'étape 5 ; recommandé : la mettre à jour dans l'étape qui
  retire chaque valeur (3 : `EndpointOAuthClaude` ; 5 : `PontStatusLine`).
- MODIF `README.md` : l. 20 (« Un `~` … signale une estimation » est faux → « Un `≥` signale un plancher… ») ; « D'où viennent
  les chiffres » l. 46-58 réécrit entièrement (sonde, secours OAuth du login Chronos, dernier exact, plancher) ; « À propos du
  token » (l. 54-56 : décrit le déchiffrement du coffre de l'app bureau et le menu « Usage exact (OAuth) » — faux) réécrit autour
  du login Chronos (jeton propre à Chronos, chiffré DPAPI dans `oauth.dat`, lecture seule de rien d'autre). Les puces de la
  section Réglages (l. 34-39 : carte « Barre de statut », « recalibrer ») restent vraies jusqu'aux étapes 4/5 : les corriger dans
  ces étapes-là.
- MODIF `CLAUDE.md` : « Contexte » (l. 1-6 restent vrais) et « Sources de données » l. 13-16 (primaire = sonde d'en-têtes ;
  secours = `/api/oauth/usage` avec le login Chronos ; tête = dernier exact ; transcripts = activité/plancher, jamais un
  pourcentage) ; « Conventions » l. 23 « Reset hebdo best-effort et recalibrable » → « reset hebdo toujours fourni par le serveur ;
  l'ancre hebdo enregistrée n'est plus que lue en secours par l'Historique » (à l'étape 4 si l'on veut la stricte synchronisation).
  Ne PAS toucher la section générée « Developer Profile » ni la mémoire hors dépôt.

**Tests**
- ADAPT `tests/Chronos.Tests/DiagnosticServiceTests.cs` :
  `Rapport_sans_token_conseille_et_n_expose_jamais_le_token` (l. 37 : assertions « Usage exact (OAuth) », « Token déchiffré : OUI »
  → réécrire en « le rapport n'expose jamais un jeton » sur la nouvelle section, en gardant les assertions PLANCHER/source/relevé) ;
  `Rapport_token_absent_le_signale_clairement` (l. 65 : « Token déchiffré : NON », « Conseil » → SUPPR-T ou réécrire sur
  « [Chaîne de données] ») ; `Le_rapport_nomme_l_etat_d_authentification_reel` (l. 84 : retirer « Token déchiffré : NON ») ;
  `Le_rapport_nomme_la_sonde_d_en_tetes_et_son_issue` (l. 129 : en-tête `[Source exacte — sonde d'en-têtes de rate-limit]` →
  nouveau libellé) ; `Un_fichier_corrompu_est_ignore_sans_casser_le_rapport` (l. 807 : `[Conseil]` → borne de fin du rapport
  choisie, ex. la dernière section) ; tous les commentaires « Token = null : la sonde réseau est gardée par `if (token is not null)` »
  deviennent faux (plus aucun appel réseau dans le rapport) — les corriger.
- NOUVEAU (dans `DiagnosticServiceTests` ou `GardeDiagnosticHistoriqueTests`) : garde **structurelle** sur le texte de
  `DiagnosticService.cs` : ne contient plus `CoffresOAuth`, `WindowsCredentialStore`, `TryReadAccessToken`, `HttpClient`,
  `api/oauth/usage`, `EnumerateDirectories(`, `[Conseil]`, `pont statusLine` ; contient `[Chaîne de données]`.
- NOUVEAU `tests/Chronos.Tests/GardeDocumentationChaineTests.cs` (DAT-05, voir § « Docs »).
- Gardes à garder vertes sans changement : `GardesPerimetreTests.Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions`
  (exige `_moniteurSessions.Inspecter(_clock.UtcNow)`, pas de `new SessionMonitor`), `Le_diagnostic_recoit_le_moniteur_du_conteneur`,
  `GardeDiagnosticHistoriqueTests` (une seule `InventaireProcessus.Relever()`, pas de `.Lire(`, `LecteurJournal.`,
  `AnalyseReleves.Analyser(`, `TimeZoneInfo.Local` ; `TimeZoneInfo? fuseau = null` présent), `NormalisationUniqueTests`
  (aucun `/ 100`, `* 100`, `FromUnixTime*`, `DateTimeOffset.TryParse` dans la nouvelle section).

### Étape 2 — Orphelins (§4, §7)

**Production**
- SUPPR `src/Chronos/Services/FiveHourWindowInference.cs`, `src/Chronos/Services/WeeklyWindow.cs`.
- MODIF `src/Chronos/ViewModels/MainViewModel.cs` : retirer `_isOAuthUsageEnabled` (l. 89) + commentaire l. 88, l'initialisation
  l. 409, la commande `ToggleOAuthUsage` (l. 760-768), les mentions dans les docs l. 114 et l. 798 (**conflit C1** : la propriété
  `ChronosSettings.OAuthUsageEnabled` reste jusqu'à l'étape 3).
- MODIF `src/Chronos/App.xaml.cs` l. 132-143 : retirer le bloc `PurgerPrefixe("desktop:")` et son commentaire ; corriger les
  renvois « l. 20 et 30 » des commentaires voisins. `ArchiveStore.PurgerPrefixe` (méthode) **reste** : non listé, testé par
  `ArchiveStorePurgeTests`, cité comme précédent par `BalayageMagasinSessions` / `EcritureEtatSession`.
- MODIF `src/Chronos/Services/Historique/BornesPlage.cs` l. 10-12 : commentaire qui cite `WeeklyWindow`/`WeeklyRecalibration`.

**Tests**
- SUPPR-T `FiveHourWindowInferenceTests.cs`, `WeeklyWindowTests.cs`.
- ADAPT `BornesPlageTests.cs` l. 75-95 (`La_derive_de_WeeklyWindow_est_mesurable`) : réécrire sans `WeeklyWindow` (garder la
  preuve DST : la fin calendaire du 25/10/2026 diffère de 7 × 24 h d'une heure, calculée inline) ou supprimer en le disant.
- ADAPT `MainViewModelTests.cs` : SUPPR-T `Initialisation_IsOAuthUsageEnabled_reflete_le_setting_par_defaut_true` (l. 631),
  `ToggleOAuthUsage_bascule_et_persiste_le_flag` (l. 639), `ToggleOAuthUsage_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer`
  (l. 655) ; ADAPT `Les_DEUX_interrupteurs_sont_INDEPENDANTS_sur_disque` (l. 897 — ne garder que la sonde, ou supprimer).
- ADAPT `ReglagesBindingTests.cs` l. 79-95 (`Assert.NotSame(vm.ToggleOAuthUsageCommand, …)`, `IsOAuthUsageEnabled`) : retirer
  la comparaison, garder la preuve que l'interrupteur de la sonde est lié à `ToggleSondeEnTetesCommand`.
- SUPPR-T `GardesPerimetreTests.Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives` (l. 83-93) ; **garder**
  `Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` (l. 26).

### Étape 3 — Jeton de l'app bureau (§1, §6 partiel) — DAT-02

**Production**
- SUPPR `src/Chronos/Services/ClaudeOAuthUsageProvider.cs`, `GatedOAuthUsageProvider.cs`, `ClaudeTokenReader.cs`,
  `IClaudeTokenReader.cs`, `WindowsCredentialStore.cs`, `InventaireMachine.cs`, `IInventaireMachine.cs`.
- MODIF `src/Chronos/App.xaml.cs` : retirer l. 408-418 (reader, `ClaudeOAuthUsageProvider`, portillon) ; chaîne l. 486-497 →
  `inner: new CompositeUsageProvider(primary: sonde, fallback: new CompositeUsageProvider(primary: ChronosOAuth, fallback:
  ClaudeUsageObjectProvider))` ; commentaire l. 475-481 (« sonde → login OAuth Chronos → pont statusLine ») ; enregistrement du
  diagnostic l. 542-565 : retirer `sp.GetRequiredService<IClaudeTokenReader>()` et le commentaire « `machine` est sauté ».
- MODIF `src/Chronos/Services/DiagnosticService.cs` : **signature** → retirer `IClaudeTokenReader tokenReader` (1er paramètre)
  et `IInventaireMachine? machine = null` ; docs XML correspondantes (dont « poll UIA 936 ms », reliquat C5 de l'inventaire).
  Nouvelle signature : `(ChronosPaths paths, SettingsService settings, IUsageProvider composite, IClock clock,
  IAuthStatus? authStatus = null, IEtatServeur? etatServeur = null, SessionMonitor? moniteurSessions = null,
  IReadOnlyList<IEtatMagasin>? magasins = null, DateTimeOffset? demarrageProcessus = null,
  IEtatReconstruction? reconstruction = null, TimeZoneInfo? fuseau = null)`.
- MODIF `src/Chronos/Services/ChronosSettings.cs` : retirer `OAuthUsageEnabled` (l. 71-74) et réécrire la doc de
  `SondeEnTetesActivee` (l. 75-83 qui s'y réfère) (**conflit C1** résolu ici).
- MODIF `src/Chronos/Models/SourceUsage.cs` : retirer `EndpointOAuthClaude` seulement (**conflit C2**) ; mettre à jour le
  commentaire « Les cinq membres » → quatre.
- MODIF `src/Chronos/Text/LibelleSource.cs` l. 30 : retirer le bras `EndpointOAuthClaude` (sinon CS0117).
- MODIF commentaires citant les types retirés (text-only, pas d'erreur de build sans génération XML, mais la garde de non-retour
  par réflexion ne les voit pas ; les retirer par cohérence) : `RateLimitHeaderUsageProvider.cs` (« motif GatedOAuthUsageProvider »,
  l. ~145, ~1283 côté tests).
- `docs/data-sources.md` §7 l. 370 : retirer `EndpointOAuthClaude` de la liste des valeurs de `source`.

**Tests**
- SUPPR-T `ClaudeOAuthUsageProviderTests.cs`, `GatedOAuthUsageProviderTests.cs`, `ClaudeTokenReaderTests.cs`,
  `Fakes/FakeClaudeTokenReader.cs`, `Fakes/FakeInventaireMachine.cs`, `Fakes/V10TestVault.cs` (utilisé seulement par
  `ClaudeTokenReaderTests`).
- ADAPT (signature `DiagnosticService` — retirer le 1er argument et `machine: new FakeInventaireMachine()`) — **sites vérifiés** :
  `CadranBindingTests.cs:60`, `CompositionRootTests.cs:90-96` (+ retirer `AddSingleton<IClaudeTokenReader>`),
  `DiagnosticServiceTests.cs` l. 47, 68, 89, 122, 151, 179, 203, 242, 269, 295, 325, 391, 432, 526, 938, 1336 (`DiagAvecMagasins`)
  et tous les helpers internes qui construisent le service, `MainViewModelTests.cs:90, 138`, `MontageReglages.cs:55`,
  `OuvreurReglagesTests.cs:115`, `OverlayWindowConfigTests.cs:26`, `ThemingTests.cs:115`.
  Les gardes textuelles qui cherchent `new DiagnosticService(` dans `App.xaml.cs` (`GardesPerimetreTests:141`,
  `GardeDiagnosticHistoriqueTests:72`) restent vertes (elles lisent `moniteurSessions:` et `fuseau:` nommés).
- ADAPT `GardesPerimetreTests.Le_journal_enveloppe_le_composite_et_la_tete_enveloppe_le_journal` l. 626-627 : **3 → 2**
  (commentaire : « 2 jusqu'au retrait du pont, 1 ensuite »).
- ADAPT `NormalisationUniqueTests.cs:41` : retirer l'exemption `ClaudeTokenReader.cs` (orpheline).
- ADAPT `LibelleSourceTests.cs:30` (InlineData `EndpointOAuthClaude`) : SUPPR la ligne.
- ADAPT `SettingsServiceTests.cs` l. 56, 75, 152 (`OAuthUsageEnabled`) ; `RateLimitHeaderUsageProviderTests.cs` l. 114-127
  (défaut `OAuthUsageEnabled` / indépendance des deux champs → ne garder que `SondeEnTetesActivee` défaut true).
- NOUVEAU (LigneJournalTests ou LecteurJournalTests) : une ligne `{"v":1,"t":"…","source":"EndpointOAuthClaude",…}` est
  **ignorée sans exception** et les lignes suivantes se lisent (`LireEnum` → `Enum.TryParse` échoue → `return false`).
- Ajouter les noms de l'étape 3 à la garde de non-retour (Pattern 2).

### Étape 4 — Recalibrage (§3)

**Production**
- SUPPR `src/Chronos/Services/WeeklyRecalibration.cs`, `src/Chronos/Services/IRecalibrationPrompt.cs`,
  `src/Chronos/ViewModels/RecalibrationViewModel.cs`, `src/Chronos/Views/RecalibrationDialog.xaml`,
  `src/Chronos/Views/RecalibrationDialog.xaml.cs`, `src/Chronos/Views/RecalibrationPrompt.cs`.
- MODIF `src/Chronos/ViewModels/MainViewModel.cs` : retirer `_prompt` (l. 33) et le paramètre **positionnel**
  `IRecalibrationPrompt prompt` (l. 375, 6ᵉ position) ; `ApplySnapshot` l. 530-535 → `SevenDay.Apply(snap.SevenDay)` ;
  retirer `Recalibrate` (l. 742-757) ; doc de classe l. 20-25 ; `_last` RESTE (lu l. 709 pour le reset 5 h local) — corriger seulement
  ses commentaires l. 48 et 528 (« pour ré-appliquer après recalibrage »).
- MODIF `src/Chronos/App.xaml.cs` l. 316-319 (`IRecalibrationPrompt`) et le commentaire l. 115-116.
- MODIF `src/Chronos/Views/Reglages/ReglagesWindow.xaml` l. 982-998 : retirer la carte « Recalibrer le reset hebdomadaire… »
  (`BoutonRecalibrerHebdo`, `RecalibrateCommand`) ; le commentaire l. 98 cite « Recalibrer ».
- MODIF `src/Chronos/Services/IOuvreurHistorique.cs:6` (`<see cref="IRecalibrationPrompt"/>` dans la doc).
- `ChronosSettings.WeeklyAnchor` **reste** (lu par `HistoriqueViewModel.cs:72, 271` et `BornesPlage` via `ISourceHistorique`) ;
  plus aucun écrivain après cette étape (vérifiable : grep `WeeklyAnchor =` = 0 hors tests).
- MODIF `README.md` l. 39 (« recalibrer le reset hebdomadaire ») ; `CLAUDE.md` l. 23 si non fait à l'étape 1.

**Tests**
- SUPPR-T `WeeklyRecalibrationTests.cs`, `Fakes/FakeRecalibrationPrompt.cs`.
- ADAPT (ctor `MainViewModel` sans `prompt`) : `CadranBindingTests.cs:57`, `MainViewModelTests.cs:93, 135` (+ helper qui reçoit
  `prompt`), `MontageReglages.cs:52`, `OuvreurReglagesTests.cs:113`, `OverlayWindowConfigTests.cs:23`, `ThemingTests.cs:112`,
  `CompositionRootTests.cs:87` (retirer `AddSingleton<IRecalibrationPrompt>`).
- SUPPR-T `MainViewModelTests` : `Recalibrate_recale_le_repli_hebdo_en_conservant_le_badge_estimee` (l. 408),
  `Recalibrate_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer` (l. 527), `Recalibrate_annule_ne_change_rien` (l. 555),
  `Recalibrate_ne_touche_pas_une_source_hebdo_exacte` (l. 578). AJOUTER : une fenêtre hebdo sans `ResetsAt` reste sans
  `ResetsAt` même avec une `WeeklyAnchor` enregistrée (plus aucun reset synthétique — méthodologie point 6 de l'inventaire).
- ADAPT `ReglagesWindowTests.cs` : l. 327 (ligne `Recalibrate` du tableau commande→section), l. 377 (libellé
  « Recalibrer le reset hebdomadaire… »), SUPPR-T `Recalibrer_est_dans_Comportement` (l. 404).
- Ajouter les noms de l'étape 4 à la garde de non-retour.

### Étape 5 — Pont statusLine + retrait de la barre (§2, §6 reste) — DAT-03 (en dernier)

**Prérequis Phase 36** (ne pas refaire) : `ArgumentsDemarrage.Trier` (liste blanche, `Services`) + `ModeDemarrage` ; lecture
tolérante de `SettingsService`. Ce que la phase 37 y change : supprimer `ArgumentsDemarrage.StatusLine` (const), le membre
`ModeDemarrage.StatusLine`, le `case` du `switch` d'`OnStartup` ; mettre à jour `ArgumentsDemarrageTests` (`--statusline` →
`ArgumentInconnu` ; `--STATUSLINE` → `ArgumentInconnu` ; préséance `--statusline --hook X` → **`Hook("X")`**, changement
assumé). Si la phase 36 a livré une forme différente, appliquer l'équivalent : « `--statusline` n'est plus dans la liste blanche ».

**Production**
- SUPPR `src/Chronos/Services/ClaudeUsageObjectProvider.cs`, `StatusLineBridge.cs`, `StatusLineInstaller.cs`,
  `IStatusLineSetup.cs`, `src/Chronos/Views/StatusLineSetup.cs`.
- MODIF `src/Chronos/App.xaml.cs` :
  - retirer `RunStatusLineBridge()` (l. 217-243) et son cas de mode ; corriger le commentaire de `SignalerSurErreurStandard`
    (l. 201-203 qui cite `RunStatusLineBridge`) ;
  - retirer `AddSingleton<ClaudeUsageObjectProvider>()` (l. 389), `StatusLineInstaller` + `IStatusLineSetup` (l. 321-326) ;
  - chaîne : `inner: new CompositeUsageProvider(primary: sp.GetRequiredService<RateLimitHeaderUsageProvider>(),
    fallback: sp.GetRequiredService<ChronosOAuthUsageProvider>())` — **un seul** `new CompositeUsageProvider(` dans le fichier ;
  - retirer `OfferOnFirstRun()` (l. 154-156) ;
  - lecture héritée de la commande interne AVANT `window.Show()` et appel de la réconciliation avec elle ; déplacer
    `LogStartupAsync` APRÈS la réconciliation (voir § « Mécanisme » et § « Journalisation ») ;
  - commentaires « (les modes --hook et --statusline sortent bien plus haut) » l. 119-124, 139-141, 145-147.
- MODIF `src/Chronos/Services/ClaudeSettingsReconciler.cs` : remplacer `StatusLineInstaller.ApplyStatusLine(root, exePath)`
  (l. 91) par le retrait ; bilan ; doc de classe (l. 18-23 : `--statusline`, `OfferOnFirstRun`).
- MODIF `src/Chronos/Services/ClaudeSettingsJson.cs` : **garder** `StatusLineMarker` (`"--statusline"`) — c'est le prédicat de
  RETRAIT ; corriger la doc l. 15-23 (« marqueur du pont » → « marqueur des anciennes barres Chronos à retirer »).
- MODIF `src/Chronos/Services/RefreshOrchestrator.cs` : retirer `_watcher`, `CreateWatcher`, `Trigger`, `OnError`,
  `RecreateWatcher`, le `using System.IO` si inutile, l'appel `CreateWatcher()` (l. 50) et le `Dispose` du `StopAsync` ; doc de
  classe (« FileSystemWatcher débouncé sur usage.json »). **Recommandé** : retirer aussi le paramètre `ChronosPaths paths` du
  constructeur (plus utilisé) — sites : `RefreshOrchestratorTests` (×5), `ArretHoteTests.cs:103`, `CadranBindingTests.cs:55`,
  `MainViewModelTests.cs:89, 131, 799, 919`, `MontageReglages.cs:50`, `OuvreurReglagesTests.cs:110`,
  `OverlayWindowConfigTests.cs:21`, `ThemingTests.cs:110` (la DI le résout seule). `RefreshOptions.Debounce` reste (coalescence).
- MODIF `src/Chronos/Services/ChronosSettings.cs` : retirer `InnerStatusLineCommand` (l. 84-87) et `StatusLinePromptDismissed`
  (l. 88-91) ; doc l. 68 (`<see cref="StatusLineInstaller"/>` le cas échéant).
- MODIF `src/Chronos/ViewModels/MainViewModel.cs` : retirer `_statusLineSetup` (l. 37), le paramètre **positionnel**
  `IStatusLineSetup statusLineSetup` (l. 376), `_isStatusLineSourceEnabled` (l. 92), l'init l. 411, `ToggleStatusLineSource`
  (l. 840-849).
- MODIF `src/Chronos/Views/Reglages/ReglagesWindow.xaml` l. 664-681 : retirer la carte « Barre de statut de Claude Code »
  (`InterrupteurBarreStatut`).
- MODIF `src/Chronos/Models/SourceUsage.cs` : retirer `PontStatusLine` (**conflit C2**) ; `Text/LibelleSource.cs` l. 31.
- MODIF `src/Chronos/Services/ChronosPaths.cs` : commentaires seulement (« usage.json » → ancre du dossier).
- MODIF `src/Chronos/Services/DiagnosticService.cs` : paramètre optionnel final pour le bilan de réconciliation + une ligne
  (§ « Journalisation ») ; la ligne « Hooks --hook installés » (section widget) peut aussi dire l'état réel de `statusLine`
  (absente / tierce / Chronos).
- Commentaires restants : `RateLimitHeaderUsageProvider.cs:10-11`, `CompositeUsageProvider.cs` (« composite interne
  statusLine »), `GardesDoctrineTests` doc (« objet d'usage sur disque »).

**Docs**
- `docs/data-sources.md` §7 l. 370 : retirer `PontStatusLine` (reste `SondeEnTetes` · `EndpointOAuthChronos`).
- `docs/publish.md` §7 (l. 127-171) : « Les modes `--hook` et `--statusline` restent multi-instances » → `--hook` seul ; le
  paragraphe réconciliation (« et la statusLine vers le nouvel exe » ; « et non dans `chronos.log`, écrit avant la
  réconciliation ») devient faux → décrire le retrait et le bilan dans `chronos.log`.
- `docs/hooks-contract.md` l. 27 : « (les modes `--hook` et `--statusline` sortent bien avant) » → `--hook`.
- `README.md` l. 34-35 (carte « barre de statut de Claude Code »).

**Tests**
- SUPPR-T `ClaudeUsageObjectProviderTests.cs`, `StatusLineBridgeTests.cs`, `StatusLineInstallerTests.cs`,
  `Fakes/FakeStatusLineSetup.cs`, `TestData/usage-valid.json`, `usage-corrupt.json`, `usage-partial.json`, `usage-ancien.json`
  (utilisés seulement par `ClaudeUsageObjectProviderTests` ; `UsageNormalizationTests.cs:86` ne cite `usage-valid.json` qu'en
  commentaire → corriger le commentaire). Migrer dans `ClaudeSettingsReconcilerTests` les cas utiles de `StatusLineInstallerTests`
  (`Uninstall_restaure_la_barre_dorigine`, `Uninstall_sans_inner_retire_completement_statusLine`,
  `Uninstall_ne_touche_pas_une_barre_tierce`, `Uninstall_retire_une_barre_Chronos_dune_autre_version`).
- ADAPT `ClaudeSettingsReconcilerTests.cs` : `Repointe_la_statusLine_perimee_et_conserve_padding` (l. 175) → **« Retire la
  statusLine Chronos périmée »** (fixture `claude-settings-pollue.json` : barre `Chronos-v2.8.1.exe` avec `padding: 2`) ;
  `Preserve_agentPushNotifEnabled_et_les_cles_racine_inconnues` (l. 169 : ordre attendu sans `"statusLine"`) ;
  `Est_un_point_fixe_la_seconde_passe_ne_produit_rien` reste vert (à revérifier) ; + fichiers témoins (§ « Validation »).
- ADAPT `CompositionRootTests.cs` : `Host_resout_et_dispose_les_singletons` (l. 49-50, 68 : `ClaudeUsageObjectProvider` comme
  inner → `FakeUsageProvider` ; l. 98-99 `IStatusLineSetup`) ; `Le_graphe_DI_resout_le_reconciliateur_de_settings_Claude`
  (l. 276 `StatusLineInstaller`) ; `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` (l. 418-422 : miroir à 1 composite
  `Composite(sonde, ChronosOAuth)` pour refléter la production).
- ADAPT ctor `MainViewModel` sans `statusLineSetup` (mêmes sites qu'à l'étape 4) + `MontageReglages.NouveauVm(barreStatut:)`.
- ADAPT `ReglagesWindowTests.cs` : l. 316 (ligne `ToggleStatusLineSource`), l. 371-372 (libellés « Barre de statut de Claude
  Code » et son aide), SUPPR-T `La_barre_de_statut_est_un_interrupteur_lie_a_sa_commande_et_a_son_etat` (l. 387) ; le test
  `Les_libelles_du_plan_sont_la_et_Source_terminal_est_renomme` garde `DoesNotContain("Source terminal")`.
- ADAPT `RefreshOrchestratorTests.cs` : SUPPR-T `Ecriture_usage_json_declenche_GetAsync` (l. 61) et
  `Error_du_watcher_entraine_recreation_sans_perdre_le_refresh` (l. 114) ; doc de classe.
- ADAPT `GardesDoctrineTests.cs` l. 218-227 : `SourceUsage.PontStatusLine` → `EndpointOAuthChronos` (le test prouve la
  conservation de la source à travers des composites imbriqués, indépendante de la chaîne réelle) ;
  `JournalRelevesTests.cs:214`, `LectureVeilleTests.cs:109-120` : remplacer `PontStatusLine` par `EndpointOAuthChronos` ;
  `LibelleSourceTests.cs:31` : SUPPR la ligne.
- ADAPT `SettingsServiceTests.cs` l. 153-154 (`InnerStatusLineCommand`, `StatusLinePromptDismissed`) **et** le test de phase 36
  sur le fichier témoin `settings-valeurs-inconnues.json` qui assertait `InnerStatusLineCommand` conservé : la clé devient un
  membre inconnu, ignoré — asserter plutôt que le reste est conservé.
- ADAPT `GardesPerimetreTests` : composite **2 → 1** ; garde de placement du verrou (réécrite par la phase 36) : retirer toute
  exigence de littéral `"--statusline"` ; NOUVEAU : « la réconciliation précède `LogStartupAsync` dans `App.xaml.cs` » et
  « `ClaudeSettingsReconciler.cs` n'appelle plus `ChronosCommand(` / `ApplyStatusLine` » (le réconciliateur ne pose ni ne repointe
  jamais une barre).
- NOUVEAU : `LigneJournal` ignore une ligne `"source":"PontStatusLine"` sans exception.
- Ajouter les noms de l'étape 5 à la garde de non-retour.

## Mécanisme de retrait de la barre (DAT-03)

### Ce qui existe et se réutilise
- Prédicat d'appartenance : `ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker)` — marqueur
  `--statusline` **et** nom de fichier `Chronos*.exe` (jamais le chemin). Reconnaît la commande réelle de l'utilisateur
  (`"C:/…/PROJET OVERLAY/Chronos-v3.4.0.exe" --statusline`, chemin entre guillemets avec espaces → `ExtractExecutable`).
- Sémantique de retrait : `StatusLineInstaller.TransformForUninstall` (l. 139-153) — si la barre est à Chronos : restaurer
  `innerCommand` par **mutation** de `command` (type, padding, clés futures conservés), sinon `root.Remove("statusLine")` ; barre
  tierce intouchée.
- Garanties E/S du réconciliateur, inchangées : fichier absent ⇒ rien (on ne crée jamais) ; `ParseOrNull` → `null` ⇒ rien ;
  comparaison sur forme **normalisée** (pas de réécriture pour une indentation) ; **sauvegarde** horodatée
  `%APPDATA%\Chronos\backups\claude-settings-AAAAMMJJ-HHMMSS[-n].json` **avant** toute écriture et seulement si une écriture va
  avoir lieu ; échec de sauvegarde ⇒ on n'écrit pas ; écriture atomique temp → `File.Move` ; rétention 5.
- L'idempotence est structurelle : après retrait, `statusLine` est absente (ou tierce après restauration) ⇒ la seconde passe ne
  produit aucune différence ⇒ `ReconcileJson` rend `null` ⇒ ni écriture ni sauvegarde.

### Code recommandé (Services, neutre)

```csharp
// ClaudeSettingsReconciler.cs — cœur PUR
public enum IssueBarreStatut { Absente, Tierce, Retiree, Restauree }

/// <summary>Bilan d'UN passage (journalisé par le diagnostic dans chronos.log).</summary>
public sealed record BilanReconciliation(
    bool Ecrit, IssueBarreStatut? Barre, string? Sauvegarde, string? Cause);   // Barre null = fichier absent/illisible

/// <summary>Retire la barre si et seulement si elle est à Chronos (marqueur --statusline + Chronos*.exe, quel que soit le
/// chemin) ; restaure la barre d'origine mémorisée par l'ancien installeur à la place, si elle existe. Barre tierce intouchée.</summary>
internal static IssueBarreStatut RetirerBarreChronos(JsonObject root, string? commandeHeritee)
{
    if (root["statusLine"] is not JsonObject sl) return IssueBarreStatut.Absente;
    var cmd = ClaudeSettingsJson.CommandOf(sl);
    if (!ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker)) return IssueBarreStatut.Tierce;
    if (!string.IsNullOrWhiteSpace(commandeHeritee)
        && !ClaudeSettingsJson.IsChronosCommand(commandeHeritee, ClaudeSettingsJson.StatusLineMarker))
    {
        sl["command"] = commandeHeritee;          // MUTATION : type, padding, clés futures conservés
        return IssueBarreStatut.Restauree;
    }
    root.Remove("statusLine");
    return IssueBarreStatut.Retiree;
}

public static string? ReconcileJson(string? settingsJson, string exePath, bool hooksWanted,
                                    string? commandeHeritee, out IssueBarreStatut? barre)
{
    barre = null;
    var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
    if (root is null) return null;                              // JAMAIS un objet vierge
    var avant = ClaudeSettingsJson.Serialize(root);
    SessionHookInstaller.ApplyHooks(root, exePath, hooksWanted); // hooks : inchangé (repointés vers l'exe courant)
    barre = RetirerBarreChronos(root, commandeHeritee);          // remplace StatusLineInstaller.ApplyStatusLine
    var apres = ClaudeSettingsJson.Serialize(root);
    return string.Equals(avant, apres, StringComparison.Ordinal) ? null : apres;
}

// Surcharge à 3 arguments conservée : les tests existants (ReconcileJson(x, Exe, hooksWanted: true)) compilent tels quels.
public static string? ReconcileJson(string? settingsJson, string exePath, bool hooksWanted)
    => ReconcileJson(settingsJson, exePath, hooksWanted, commandeHeritee: null, out _);
```

- `Reconcile(bool hooksWanted, string? commandeHeritee = null)` garde son `bool` de retour (tests existants) et pose une
  propriété `DernierBilan` (record immuable, assignation atomique) : `Sauvegarde` = chemin réel écrit par `Sauvegarder()`
  (le faire rendre le chemin), `Cause` = « fichier absent » / « illisible — rien écrit » / « sauvegarde impossible — rien écrit » /
  exception attrapée.
- Lecture héritée, PURE et testable : `public static string? CommandeInterneHeritee(string? chronosSettingsJson)` → lit la clé
  `InnerStatusLineCommand` (insensible à la casse, comme `SettingsService`) via `JsonNode` ; `null` si absente, non textuelle,
  vide, ou JSON illisible.

### Câblage dans `App.OnStartup` (ordre exigé)
1. Après `_host` construit et `settings` résolu (l. 112), **avant** `window.Show()` (l. 117) :
   `var commandeHeritee = ClaudeSettingsReconciler.CommandeInterneHeritee(LireSansLever(paths.SettingsFile));`
   (`ChronosPaths` du conteneur, jamais `Assembly.Location`). Raison : `SnapToNearestCorner` peut faire
   `Save(Load() with …)` dès `SourceInitialized`/`DpiChanged`, ce qui effacerait la clé devenue inconnue.
2. `window.Show()`.
3. `Reconcile(settings.SessionsWidgetEnabled, commandeHeritee)` (best-effort, `try/catch` conservé).
4. **Puis** `_ = DiagnosticService.LogStartupAsync()` (déplacé depuis l. 108).
5. Balayage, `ShowIfEnabled()` inchangés ; `OfferOnFirstRun()` supprimé.

Cas réel attendu au 1er lancement 3.5 : `commandeHeritee = null` ⇒ `statusLine` **retirée** ; les hooks repointés vers
`Chronos-v3.5.0.exe` dans la même écriture ; une sauvegarde unique. Second lancement : `Absente`, rien écrit, aucune sauvegarde.

### Journalisation dans `chronos.log`
`chronos.log` n'a qu'un écrivain au démarrage : `LogStartupAsync` → `File.WriteAllText(…, "(log automatique au démarrage)\n" +
report)` (`DiagnosticService.cs:122`). Il est lancé avant la réconciliation et attend `_composite.GetAsync` (réseau possible)
avant d'écrire : toute ligne ajoutée par le réconciliateur serait écrasée ; c'est ce que `docs/publish.md` §7 avoue
(« `chronos.log`, écrit avant la réconciliation »). Recommandation : (a) réconciliation avant `LogStartupAsync` (câblage
ci-dessus) ; (b) `DiagnosticService` reçoit le réconciliateur (paramètre optionnel final, même instance DI) et écrit une ligne,
p. ex. dans la section widget/réglages Claude Code :
`Réglages Claude Code (ce lancement) : barre de statut Chronos retirée — sauvegarde claude-settings-20261005-091200.json`
/ `aucune barre Chronos (rien à retirer)` / `barre tierce laissée intacte` / `fichier illisible — rien écrit`.
Le rapport à la demande (Réglages → Diagnostic) dit la même chose. Ne pas ajouter de second écrivain de `chronos.log`.

### Pièges propres au retrait
- **Sessions Claude Code déjà ouvertes** : la config est lue au démarrage d'une session ; elles continuent d'appeler
  `Chronos-v3.4.0.exe --statusline` (l'ancien exe garde son pont) jusqu'à leur redémarrage. Si un ancien exe a été supprimé, la
  barre de ces sessions est vide — sans gravité. Un `--statusline` qui atteint l'exe 3.5 sort en silence (garde phase 36), donc
  barre vide, jamais d'overlay ni de MessageBox « tourne déjà ».
- **Retour arrière vers 3.4** : les réglages 3.5 ont perdu `StatusLinePromptDismissed` ⇒ une 3.4 relancée reproposerait le pont
  au premier lancement. Acceptable (downgrade), à mentionner dans les notes de version (phase 43).
- **`Chronos.Tests` = `testhost.exe`** : injecter `exePath` (déjà prévu par le ctor) — sinon le prédicat ne reconnaît pas les
  entrées posées et l'idempotence est invérifiable (doc du ctor l. 50-55).
- Tous les tests du réconciliateur restent sous `Path.GetTempPath()` (garde `Aucun_test_ne_cible_le_vrai_settings_du_profil`).

## Section « Chaîne de données » (DAT-04) — contenu recommandé

Libellés = discrétion ; contraintes = vérifiées. Ordre : la section remplace, au même endroit (après la version), les deux
sections « Source exacte » actuelles. Rien n'y fait d'appel réseau ni de recherche disque large : elle lit l'état déjà connu.

```
[Chaîne de données]
  Chaîne : sonde d'en-têtes → secours OAuth du login Chronos (meilleure source PAR FENÊTRE) → journal → dernier exact → cadran
  Sonde d'en-têtes : <lignes actuelles : Interrupteur / Dernière sonde / En-têtes « unified » reconnus / Dépassement>
  Secours OAuth (login Chronos) : <Connecté : … ; État d'authentification : …>  (seulement si la sonde n'a pas de chiffre)
  Dernier exact persisté : <LigneMagasin(NomsMagasins.DernierExact, LastExactFile)> ; fraîcheur : exact jusqu'à
      <DoctrineFraicheur.LimiteAge en s, dérivé> après le relevé, puis « encore valide » sans activité Claude Code,
      sinon plancher « ≥ X % » (seul chiffre non exact)
  Journal : <âge de la dernière écriture du journal (IEtatMagasin JournalReleves), « ALERTE — journal muet » si applicable>
```
Contraintes :
- Le snapshot vient du **seul** appel `_composite.GetAsync` en tête de `BuildReportAsync` (ne pas relancer la chaîne : double
  sonde = double dépense ; test `Le_rapport_interroge_la_chaine_AVANT_de_decrire_la_sonde`).
- Cadences et seuils **dérivés** des constantes (`RateLimitHeaderUsageProvider.CadenceNominale`, `DoctrineFraicheur.LimiteAge`,
  `JournalReleves.SeuilMuet`), jamais recopiés.
- Ne pas relire le journal dans ce fichier (`JournalReleves.DernierT(...)` lit la queue du fichier : à éviter, gardes
  `GardeDiagnosticHistoriqueTests` / doctrine D-35-10) — l'âge de la dernière écriture et les sources par fenêtre (déjà dites
  dans « Ce qui est affiché maintenant ») suffisent.
- Aucune conversion d'unité hors `UsageNormalization` (`NormalisationUniqueTests`).
- La section `[Magasins persistants]` et `[Journal d'historique]` restent (tests d'ordre l. 1357, 1463, 1601).
- **Mesure (critère 3, « ≈ 17 s → mesuré et consigné »)** : ajouter en fin de rapport une ligne
  `Rapport construit en N ms (dont chaîne de données M ms)` via `Stopwatch` (instant d'horloge réelle, pas `IClock` — c'est une
  durée de calcul). Elle se consigne d'elle-même dans `chronos.log` à chaque lancement et dans le diagnostic à la demande ; un test
  l'asserte présente (format), sans seuil de temps flottant. Le chiffre réel avant/après se relève au constat (phase 43) ou par
  « ↻ Actualiser » ; la purge supprime `CoffresOAuth` (17 703 ms mesurés), il ne reste que `InventaireProcessus.Relever()`,
  l'énumération bornée des `.jsonl` (5 000 max) et la lecture des fichiers d'état.

## Docs (DAT-05)

### Contraintes des gardes existantes sur `docs/data-sources.md` (vérifiées)
| Garde | Exige |
|---|---|
| `ContratHooksDocumenteTests.Les_sources_disent_que_le_transcript_d_un_sous_agent_est_un_signal_de_travail` | une section `## 2.` contenant **mot pour mot** « Lu par le widget de sessions depuis la phase 30.1 (SUB-01) » ; `## 6.` avec « `subagents/agent-*.jsonl` », « SUB-01 », « jamais une ligne » |
| `ContratAppBureauDocumenteTests` | `## 6.` contient « desktop-app-sessions.md » |
| `ContratJournalDocumenteTests` | titre exact `## 7. Journal d'historique`, ≥ N lignes, les 11 champs et 6 événements entre accents graves, « 24 mois », « strictement croissant », « `(t, source)` », « FileMode.Append », « virtualisée », « 2026-09-13 » ; `publish.md` contient « une seule instance » |
| `ContratAgregatsDocumenteTests` | titre exact `## 8. Agrégats de tokens` ; « HYP-4 », « cleanupPeriodDays », « 30 » ; le §7 contient « §8 » ; **la dernière ligne non vide du document contient « §8 »** |
| `GardeDocumentationHistoriqueTests` | titre exact `## 9. Lecture par la fenêtre Historique` + sous-partie « ### Écarts connus des maquettes » |

### Plan de réécriture recommandé
- En-tête (l. 1-20) : retirer « Capturé le 2026-07-08 … contrat statusLine », « Cette phase est DOCUMENTAIRE ». Nouvel en-tête :
  daté 2026-10, « méthodologie unique », schéma de la chaîne.
- `## 1. La chaîne exacte — source → cadran` : sonde d'en-têtes (1 requête / 300 s, statut serveur, dépassement) ; secours
  `/api/oauth/usage` avec le jeton du login Chronos ; composite PAR FENÊTRE, sonde prioritaire (`Best()` ne retient le repli que
  s'il est strictement plus fiable) ; journal (voit l'inner brut) ; tête `LastExactUsageProvider` + `DoctrineFraicheur`.
- `## 2. Transcripts JSONL — activité et plancher, jamais un pourcentage` : garder **intact** le paragraphe « **Lu par le widget
  de sessions depuis la phase 30.1 (SUB-01)** … » (actuel l. 217) ; dire que les tokens servent au plancher et à l'Historique.
- `## 3. Ce qui est exact, ce qui ne l'est pas` : exact = sonde, secours OAuth, dernier exact frais (≤ `LimiteAge` = 360 s) ou
  « encore valide » (aucune activité depuis) ; **non exact = seulement le plancher « ≥ X % »** ; indisponible sinon ; resets
  toujours serveur (plus d'ancre manuelle ; l'ancre hebdo déjà enregistrée n'est lue qu'en secours par l'Historique).
- `## 4. Hypothèses & points de fragilité` : famille d'en-têtes « unified » non documentée ; endpoint OAuth non documenté ;
  modèle de la sonde ; coût.
- `## 5. Sources retirées en 3.5` (optionnel, une ligne chacune, sans noms de classes si la garde doc les interdit — voir
  ci-dessous) ou fusion dans §4. Les ancres `#4-hypothèses--points-de-fragilité` citées ailleurs doivent rester cohérentes.
- §6, §7 (sauf la ligne `source`), §8, §9 : intacts ; mettre à jour la ligne finale (garder « §8 ») pour dater la réécriture.

### Nouvelle garde documentaire `GardeDocumentationChaineTests` (exigée par le critère 4)
- Lit via `AssemblyMetadata("CheminDocsChronos")` (et `README.md`, `CLAUDE.md` à la racine : `Path.Combine(docs, "..")`, motif
  `ContratAppBureauDocumenteTests.LireVoisin`).
- **Rougit** si `README.md`, `CLAUDE.md` ou `docs/data-sources.md` citent : `ClaudeOAuthUsageProvider`, `GatedOAuthUsageProvider`,
  `ClaudeTokenReader`, `WindowsCredentialStore`, `InventaireMachine`, `ClaudeUsageObjectProvider`, `StatusLineBridge`,
  `StatusLineInstaller`, `EndpointOAuthClaude`, `PontStatusLine`, `WeeklyRecalibration`, « Usage exact (OAuth) »,
  « Estimation (repli) », « estimation par transcripts » ; et (à partir de l'étape 5) `usage.json`, « pont statusLine »,
  `--statusline` hors d'un paragraphe explicitement historique. Exiger aussi le positif : `data-sources.md` contient
  « plancher » et « ≥ » dans la section de méthodologie ; README « ## D'où viennent les chiffres » nomme la sonde d'en-têtes.
- Anti-mutisme : le fichier existe et fait plus de N lignes.
- Faire grandir la liste à l'étape 5 (chaque commit reste vert).
- Ne pas viser le mot « estimation » seul : `CLAUDE.md` l. 22/42/51 (« ne jamais présenter une estimation comme exacte ») est une
  règle de doctrine à garder.

## Don't Hand-Roll

| Problème | Ne pas écrire | Utiliser | Pourquoi |
|---|---|---|---|
| Reconnaître une barre Chronos | comparaison de chemin d'exe | `ClaudeSettingsJson.IsChronosCommand(cmd, StatusLineMarker)` | le chemin varie par version (cause des 25 groupes de hooks) ; guillemets/espaces gérés |
| Lire/écrire `~/.claude/settings.json` | `JsonDocument` + concaténation, ou `new JsonObject()` de repli | `ParseOrNull` / `Serialize` / `CommandOf` | commentaires, virgules traînantes, clés dupliquées, accents littéraux, ordre des clés |
| Sauvegarde + écriture | nouveau code de copie | `ClaudeSettingsReconciler.Sauvegarder` + `WriteAtomic` | horodatage désambiguïsé, rétention, abandon si la sauvegarde échoue |
| Libellés de source / provenance | nouveau mapping | `Text/LibelleSource.Format` / `Provenance` | un seul vocabulaire (infobulle, diagnostic) |
| Tri des arguments | test sur `e.Args` dans `OnStartup` | `ArgumentsDemarrage.Trier` (phase 36) | liste blanche ; retirer une ligne suffit |

## Runtime State Inventory

| Catégorie | Trouvé | Action |
|---|---|---|
| Données stockées | `%APPDATA%\Chronos\settings.json` : clés `OAuthUsageEnabled`, `InnerStatusLineCommand` (null), `StatusLinePromptDismissed`, `WeeklyAnchor` (2026-07-11). `usage.json` (figé au 10/07). Journal `historique\releves-*.jsonl` : **zéro** ligne `EndpointOAuthClaude`/`PontStatusLine` (inventaire). `last-exact.json` : n'écrit pas la source (reconstruite `MagasinDernierExact`). `archived.json` vide de `desktop:` | Code seulement : clés inconnues ignorées puis effacées au premier `Save()` (tolérance phase 36) ; **lire `InnerStatusLineCommand` avant** ; `WeeklyAnchor` conservé (lu) ; `usage.json` laissé sur disque (hors liste) ; journal : lignes inconnues sautées (test). Les enums sont écrits **par nom** (`JsonStringEnumConverter`) : retirer des membres au milieu de `SourceUsage` ne corrompt rien de persisté |
| Config de services vivants | `~/.claude/settings.json` : `statusLine` → `Chronos-v3.4.0.exe --statusline` ; hooks Chronos | **Migration de données** au 1er lancement 3.5 (retrait + sauvegarde) ; hooks repointés comme aujourd'hui |
| État enregistré dans l'OS | Raccourci autostart `shell:startup` (vise l'exe qui l'a créé) ; mutex `Local\Chronos-overlay` | Aucun changement ; rappeler en phase 43 de réactiver l'autostart depuis l'exe 3.5 |
| Secrets / variables | `oauth.dat` (login Chronos, DPAPI) gardé ; coffre de l'app bureau : plus jamais lu | Aucun ; plus aucun déchiffrement du jeton d'une autre application |
| Artefacts | Anciens `Chronos-v3.x.exe` à la racine du dépôt ; sessions Claude Code ouvertes qui appellent l'ancien exe | Garder l'ancien exe tant que des sessions d'avant la 3.5 tournent (doc publish.md §7) |

## Common Pitfalls

### Pitfall 1 : commit qui ne compile pas seul
**Ce qui arrive :** `OAuthUsageEnabled` supprimé à l'étape 2 (lu par le portillon), `PontStatusLine` à l'étape 3 (produit par le
pont), bras de `switch` de `LibelleSource` oubliés (CS0117/CS8509), `using`/fakes orphelins.
**Éviter :** conflits C1-C3 ; après chaque étape, `grep -rn "<NomRetiré>" src tests` = 0 hors garde de non-retour.

### Pitfall 2 : « 0 avertissement » cassé par un champ mort
**Éviter :** retirer les champs privés devenus inutiles (`_tokenReader`, `_machine`, `_prompt`, `_statusLineSetup`, `_watcher`)
dans la même étape que leur dernier lecteur.

### Pitfall 3 : variable `claudeSettings` du diagnostic
Déclarée dans la section pont (l. 232), relue l. 520 : la supprimer avec la section ⇒ CS0103. Redéclarer.

### Pitfall 4 : la doc dépasse ou contredit les gardes
Renuméroter `data-sources.md` ou déplacer la phrase SUB-01 hors du `## 2.` rougit `ContratHooksDocumenteTests` ; la dernière
ligne doit contenir « §8 ». Travailler section par section, lancer `--filter "FullyQualifiedName~Contrat|FullyQualifiedName~GardeDocumentation"`.

### Pitfall 5 : bilan de retrait perdu
Ligne écrite par le réconciliateur dans `chronos.log` puis écrasée par `LogStartupAsync`. Réordonner + passer par le rapport.

### Pitfall 6 : clé `InnerStatusLineCommand` effacée avant lecture
Un `Save()` (placement/DPI) avant la réconciliation efface la clé devenue inconnue. Capturer avant `window.Show()`.

### Pitfall 7 : garde de placement du verrou (phase 36) qui cherche encore `"--statusline"`
Retirer cette exigence à l'étape 5 ; garder `--hook`, galeries, et l'ordre « tri avant verrou avant Host ».

### Pitfall 8 : `MainViewModel` / `DiagnosticService` construits à la main
Les sites de test sont listés ci-dessus ; `CompositionRootTests` est un **miroir** d'`App.ConfigureServices` : le tenir à jour à
chaque étape (sinon la garde DI cesse de prouver le graphe réel).

## Code Examples

### Test témoin du retrait (motif `ClaudeSettingsReconcilerTests`)
```csharp
// Source : motif ClaudeSettingsReconcilerTests.cs (Exe = @"C:\Apps\Chronos.exe", chemins temp)
[Fact]
public void Retire_la_barre_Chronos_de_l_utilisateur_et_laisse_le_reste()
{
    const string entree = """
    { "hooks": {}, "agentPushNotifEnabled": true,
      "statusLine": { "type": "command",
        "command": "\"C:/Users/X/Documents/PROJET OVERLAY/Chronos-v3.4.0.exe\" --statusline" } }
    """;
    var apres = ClaudeSettingsReconciler.ReconcileJson(entree, Exe, hooksWanted: false, commandeHeritee: null, out var barre);
    Assert.Equal(IssueBarreStatut.Retiree, barre);
    Assert.False(Racine(apres!).ContainsKey("statusLine"));
    Assert.True(Racine(apres!)["agentPushNotifEnabled"]!.GetValue<bool>());
    Assert.Null(ClaudeSettingsReconciler.ReconcileJson(apres, Exe, hooksWanted: false));   // point fixe
}
```

### Câblage de la chaîne finale (étape 5)
```csharp
// Source : App.xaml.cs l. 486-498 (forme actuelle), réduite à la chaîne cible
services.AddSingleton(sp => new JournalisationUsageProvider(
    inner: new CompositeUsageProvider(
        primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),   // sonde : seule porteuse du statut serveur
        fallback: sp.GetRequiredService<ChronosOAuthUsageProvider>()),     // secours exact
    journal: sp.GetRequiredService<JournalReleves>(),
    etatServeur: sp.GetRequiredService<IEtatServeur>(),
    authStatus: sp.GetRequiredService<IAuthStatus>(),
    clock: sp.GetRequiredService<IClock>()));
```
La garde `Le_journal_enveloppe_le_composite…` exige les littéraux `new JournalisationUsageProvider(` et
`inner: new CompositeUsageProvider(` (dans cet ordre) — la forme ci-dessus les conserve.

## State of the Art

| Ancien | Actuel (après phase 37) | Quand | Impact |
|---|---|---|---|
| 3 composites : sonde → OAuth Chronos → (OAuth app bureau gated → pont statusLine) | 1 composite : sonde → OAuth Chronos | 3.5.0 | diagnostic sans coffres (≈ 17 s gagnées), plus de déchiffrement d'un jeton tiers |
| Réconciliateur repointe la barre vers l'exe courant | Réconciliateur retire la barre Chronos (restaure l'éventuelle barre d'origine) | 3.5.0 | Claude Code n'affiche plus de barre Chronos |
| Recalibrage hebdo manuel (ancre + 7 × 24 h) | Reset toujours serveur ; ancre lue seulement par l'Historique | 3.5.0 | carte « Recalibrer… » retirée |
| `chronos.log` écrit avant la réconciliation | Réconciliation puis rapport (bilan inclus) | 3.5.0 | migration observable dans le log |

## Open Questions

1. **Forme exacte livrée par la phase 36** (noms `ArgumentsDemarrage` / `ModeDemarrage`, réécriture des gardes de placement).
   - Connu : recommandation 36-RESEARCH (liste blanche, `Trier`, `case ModeDemarrage.StatusLine`).
   - Inconnu : le code final. Recommandation : le plan de l'étape 5 commence par relire `App.xaml.cs` et `ArgumentsDemarrage*.cs`.
2. **Où écrire la ligne de bilan** dans le rapport (section widget vs petite section « [Réglages de Claude Code] »).
   - Discrétion ; contrainte : une seule ligne, dans `chronos.log`, mêmes mots dans le diagnostic à la demande.
3. **Retirer `ChronosPaths` du ctor de `RefreshOrchestrator`** (propreté) vs le garder (moins de churn).
   - Recommandation : le retirer à l'étape 5 (12 sites mécaniques) ; acceptable de le garder si le plan veut un diff minimal.
4. **Mesure « avant »** du diagnostic sur la vraie machine : non mesurable sans lancer l'exe 3.4 ; le chiffre « ≈ 17 s » vient
   de la mesure de phase 20 (17 703 ms). La ligne de durée rend la mesure « après » automatique.

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build/tests | ✓ | 10.0.201 (cible net8.0-windows) | — |
| Runtime .NET 8 Desktop (tests) | `dotnet test` | ✓ (suites précédentes vertes) | — | — |
| `~/.claude/settings.json` réel | aucune (tests sur fichiers témoins temp) | — | — | — |

Aucune dépendance externe nouvelle. Le constat sur l'exe réel (barre disparue, `--statusline` silencieux) est manuel, en phase 43.

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`), net8.0-windows |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemins `CheminSourcesChronos` / `CheminDocsChronos` injectés) |
| Commande rapide | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~GardesDoctrineTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~ServicesLayerPurityTests"` |
| Suite complète | `dotnet build Chronos.sln` (0 avertissement) puis `dotnet test Chronos.sln` (≈ 2-3 min) |

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande | Fichier |
|---|---|---|---|---|
| DAT-02 | aucun maillon retiré ne subsiste (réflexion, liste croissante par étape) ; membres `ChronosSettings`/`SourceUsage` absents | garde | `--filter "FullyQualifiedName~GardesPerimetreTests"` | ❌ W0 (nouveau test dans `GardesPerimetreTests.cs`) |
| DAT-02 | un seul `new CompositeUsageProvider(` dans `App.xaml.cs` (3 → 2 → 1), journal entre tête et composite | garde | idem | ✅ à adapter |
| DAT-02 | graphe DI réel résolu (miroir) | intégration | `--filter "FullyQualifiedName~CompositionRootTests"` | ✅ à adapter |
| DAT-02 | doctrine « exact ou rien » verte à chaque commit | garde | `--filter "FullyQualifiedName~GardesDoctrineTests"` | ✅ |
| DAT-02 | un journal avec `EndpointOAuthClaude` / `PontStatusLine` se relit sans erreur | unit | `--filter "FullyQualifiedName~LigneJournalTests\|FullyQualifiedName~LecteurJournalTests"` | ❌ W0 |
| DAT-02 | `WeeklyAnchor` toujours lu par l'Historique ; aucun reset synthétique au cadran | unit | `--filter "FullyQualifiedName~HistoriqueViewModelTests\|FullyQualifiedName~MainViewModelTests"` | ✅ + ❌ (nouveau cas MainViewModel) |
| DAT-03 | fichiers témoins : barre Chronos → retirée + sauvegarde ; tierce intacte ; sans barre → rien ; illisible → octet pour octet, sans sauvegarde ; inner non nul → restauré ; 2ᵉ passe → rien | unit + E/S temp | `--filter "FullyQualifiedName~ClaudeSettingsReconcilerTests"` | ✅ à étendre |
| DAT-03 | lecture héritée `InnerStatusLineCommand` (présente, null, absente, illisible) | unit | idem | ❌ W0 |
| DAT-03 | bilan journalisé (ligne dans le rapport) ; réconciliation avant `LogStartupAsync` ; réconciliateur sans `ChronosCommand(` | unit + garde textuelle | `--filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests"` | ❌ W0 |
| DAT-03 | `--statusline` → `ArgumentInconnu` | unit | `--filter "FullyQualifiedName~ArgumentsDemarrageTests"` | ✅ (phase 36) à adapter |
| DAT-03 | carte « Barre de statut » absente des réglages | WPF | `--filter "FullyQualifiedName~ReglagesWindowTests"` | ✅ à adapter |
| DAT-04 | section `[Chaîne de données]` (sonde, secours OAuth, dernier exact, journal) ; sections mortes absentes ; ligne de durée | unit | `--filter "FullyQualifiedName~DiagnosticServiceTests"` | ✅ à adapter + ❌ |
| DAT-04 | `DiagnosticService.cs` ne contient plus coffres/HTTP/credential store | garde textuelle | `--filter "FullyQualifiedName~GardeDiagnosticHistoriqueTests"` | ❌ W0 |
| DAT-05 | docs sans source retirée ; méthodologie dit « seul le plancher ≥ n'est pas exact » | garde doc | `--filter "FullyQualifiedName~GardeDocumentationChaineTests"` | ❌ W0 |
| DAT-05 | gardes doc existantes vertes (§2 SUB-01, §6, §7, §8, §9, dernière ligne) | garde doc | `--filter "FullyQualifiedName~Contrat\|FullyQualifiedName~GardeDocumentation"` | ✅ |

### Sampling Rate
- **Par tâche :** commande rapide filtrée + le filtre du fichier touché.
- **Par commit d'étape (= par plan) :** `dotnet build Chronos.sln` 0 avertissement + `dotnet test Chronos.sln` complet (le
  critère « gardes vertes après **chacun** des cinq commits » l'exige).
- **Avant `/gsd:verify-work` :** suite complète verte ; `git revert --no-commit <commit d'étape>` + build en essai à blanc
  optionnel pour prouver la réversibilité d'une étape (puis `git revert --abort`).

### Wave 0 Gaps
- [ ] `GardesPerimetreTests.Aucun_maillon_retire_de_la_chaine_ne_subsiste` (créé étape 2, enrichi 3/4/5)
- [ ] `tests/Chronos.Tests/GardeDocumentationChaineTests.cs` (étape 1, enrichi étape 5)
- [ ] Garde structurelle « diagnostic sans coffres » (étape 1)
- [ ] Cas `LigneJournal` « source retirée ignorée » (étapes 3 et 5)
- [ ] Fichiers témoins du retrait + `CommandeInterneHeritee` dans `ClaudeSettingsReconcilerTests` (étape 5) — fixtures inline ou
      `TestData/claude-settings-barre-*.json`
- [ ] Garde textuelle « réconciliation avant `LogStartupAsync` » (étape 5)

## Sources

### Primary (HIGH confidence)
- Code du dépôt (lu le 2026-10-03) : `src/Chronos/App.xaml.cs`, `Services/DiagnosticService.cs`, `Services/ClaudeSettingsReconciler.cs`,
  `Services/StatusLineInstaller.cs`, `Services/ClaudeSettingsJson.cs`, `Services/RefreshOrchestrator.cs`, `Services/ChronosPaths.cs`,
  `Services/ChronosSettings.cs`, `Services/SettingsService.cs`, `Services/OverlayController.cs`, `Models/SourceUsage.cs`,
  `Services/Historique/LigneJournal.cs`, `ViewModels/MainViewModel.cs`, `Views/StatusLineSetup.cs`, `Views/Reglages/ReglagesWindow.xaml`.
- Tests : `GardesPerimetreTests`, `GardesDoctrineTests`, `CompositionRootTests`, `ServicesLayerPurityTests`, `NormalisationUniqueTests`,
  `GardeDiagnosticHistoriqueTests`, `Contrat*DocumenteTests`, `GardeDocumentationHistoriqueTests`, `DiagnosticServiceTests`,
  `ClaudeSettingsReconcilerTests`, `ReglagesWindowTests`, `MontageReglages`, `RefreshOrchestratorTests`.
- `.zeus/reports/cycle2/liste-purge.md` (validée), `inventaire-purge-donnees.md`, `.zeus/DESIGN_PLAN_CYCLE2.md` §6,
  `.zeus/reports/llm-council-2026-10-03.md` §6, `.planning/phases/36-socle/36-CONTEXT.md` et `36-RESEARCH.md`.
- État réel (lecture seule) : `~/.claude/settings.json` (`statusLine` → `Chronos-v3.4.0.exe --statusline`) ; `%APPDATA%\Chronos\settings.json`.

### Secondary / Tertiary
- Aucune : phase sans bibliothèque nouvelle ; aucune recherche web nécessaire.

## Metadata

**Confidence breakdown :**
- Inventaire et cascades : HIGH (chaque site vérifié par grep, numéros de ligne relevés le 2026-10-03 — ils dériveront dès la
  phase 36 ; se fier aux noms)
- Mécanisme de retrait : HIGH (code existant réutilisé ; course `chronos.log` vérifiée dans `DiagnosticService.cs:122` et `publish.md` §7)
- Forme de la garde d'arguments : MEDIUM (dépend du code final de la phase 36)

**Research date :** 2026-10-03
**Valid until :** fin de la phase 37 (les numéros de ligne bougent avec la phase 36)
