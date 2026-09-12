---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
verified: 2026-09-12T00:00:00Z
status: human_needed
score: 5/5 must-haves verified (code), 1 item requires human production check
human_verification:
  - test: "Saturer réellement la fenêtre 5 h (ou 7 j) avec un jeton OAuth VALIDE, puis ouvrir le menu Diagnostic pendant le 429."
    expected: "Le diagnostic continue d'afficher des chiffres exacts pour les deux fenêtres, plus le statut serveur, pendant que l'API répond 429 — au lieu de basculer sur « indisponible »."
    why_human: "Non vérifiable par l'automatisation : exige un jeton OAuth valide (celui de la machine est expiré depuis 2026-07-12) ET un compte réellement saturé au moment du test. La famille d'en-têtes anthropic-ratelimit-unified-* est absente de la documentation publique Anthropic — seule une réponse serveur réelle peut confirmer sa présence et son orthographe exacte. Le plan 18-06 documente ce protocole comme checkpoint humain bloquant, et HDR-02 n'est explicitement PAS coché « prouvé en production » avant cette vérification (18-VALIDATION.md, section Manual-Only Verifications)."
---

# Phase 18 : Source exacte par en-têtes de rate-limit — Verification Report

**Phase Goal:** Chronos dispose d'une source exacte supplémentaire, en tête de chaîne, qui répond même quand
l'API est en 429 — précisément l'instant où l'overlay sert le plus — et qui apporte deux informations que
personne d'autre ne donne : le statut serveur et le dépassement.

**Verified:** 2026-09-12
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (5 Success Criteria de la ROADMAP)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Chiffres exacts sans pont ni fichier (HDR-01) | ✓ VERIFIED | `RateLimitHeaderUsageProvider.GetAsync` envoie `POST /v1/messages` (`max_tokens:1`, `claude-haiku-4-5`) et lit `anthropic-ratelimit-unified-{5h,7d}-utilization/reset` via `UsageNormalization`, sans lire le corps HTTP. Câblée en `primary` (App.xaml.cs:339-348). Test `RateLimitHeaderUsageProviderTests` couvre la production de deux fenêtres exactes depuis les en-têtes seuls. |
| 2 | Répond encore en saturation (HDR-02) — **code** | ✓ VERIFIED (code) | Dans `GetAsync`, les en-têtes sont lus via `LireEnTetes(resp, now)` **avant** tout `if (code == 429)` (RateLimitHeaderUsageProvider.cs:255-304). `EnsureSuccessStatusCode` n'apparaît nulle part dans `src/Chronos/Services/*.cs` (grep vide). Le cas « 429 sans en-tête » est une branche nommée `SaturationSansEnTetes`, testée par `Un_429_muet_n_invente_rien` (Assert.Null sur Utilization des deux fenêtres, `Reliability.Unavailable`, jamais 0 ni 100). |
| 2b | Répond encore en saturation (HDR-02) — **production réelle** | ? HUMAN NEEDED | Le jeton OAuth de la machine est expiré depuis 2026-07-12 et aucun compte saturé n'est disponible pour l'automatisation. Voir `human_verification` ci-dessus et 18-VALIDATION.md (« HDR-02 ne doit pas être coché prouvé en production avant la vérification manuelle »). C'est un flag connu et honnêtement documenté, pas un gap. |
| 3 | Statut serveur et dépassement (HDR-03, HDR-04) | ✓ VERIFIED | `StatutServeur` est un enum à 4 membres incluant `NonReconnu` (Models/StatutServeur.cs:14). `StatutServeurTexte.DepuisEnTete` ne retombe JAMAIS sur `Autorise` pour une valeur inconnue (retourne `NonReconnu`), et distingue `null` (absent) de `NonReconnu` (présent, illisible) — testé par `Un_statut_inconnu_ne_se_devine_pas` et `Les_deux_inconnus_sont_des_valeurs_distinctes`. Les deux familles d'en-têtes (fenêtre + dépassement) sont lues SANS branche exclusive (`LireEnTetes` appelle `LireDepassement` ET `LireFenetre` pour les deux fenêtres, sans `elif`). Affiché : `WindowGaugeViewModel.TexteStatutServeur`/`HasStatutServeur`, `MainViewModel.TexteEtatSonde`, `SettingsWindow.xaml` ligne d'état. |
| 4 | Aucune divergence d'unité (HDR-05) | ✓ VERIFIED | `UsageNormalization.cs` est le point unique (12 sites rapatriés selon 18-VALIDATION.md, exports conformes au must_have du plan 18-01). Garde de non-retour `NormalisationUniqueTests` balaie le texte source de `Services/`+`Models/` via un chemin injecté par MSBuild, avec 4 exemptions nominatives documentées, et échoue si le chemin n'existe pas (pas de mise en sourdine possible). Piège fr-FR gravé : `UsageNormalizationTests` force `CultureInfo.CurrentCulture = fr-FR`, asserte D'ABORD `Assert.False(double.TryParse("0.63", out _))` (le piège) PUIS le remède `FractionDepuisTexteFraction`. |
| 5 | Coût maîtrisé et annoncé (HDR-06) | ✓ VERIFIED | `CadenceNominale = 300s` est une constante publique verrouillée par test (frein testé : 2 appels dans la même minute → 1 seul envoi HTTP, vérifié aussi en l'absence de cache dans `Un_429_muet_n_invente_rien`). Réglages : `SettingsWindow.xaml:194` affiche littéralement « consomme une micro-requête sur ton compte toutes les 5 min (≈ 288/jour) » — libellé honnête, pas minimisé. |

**Score:** 5/5 critères vérifiés au niveau du code ; 1 sous-item (HDR-02 en production réelle) explicitement renvoyé à vérification humaine, comme le prévoit la documentation de la phase elle-même.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/UsageNormalization.cs` | Point unique de normalisation | ✓ VERIFIED | 138 lignes, exports conformes (FractionDepuisFraction/Pourcentage/TexteFraction, InstantDepuisEpochSecondes/Millisecondes/TexteEpoch/Iso, PourcentagePourAffichage, PlancherEpoch). Classe pure, aucune I/O, aucun type WPF. |
| `src/Chronos/Services/RateLimitHeaderUsageProvider.cs` | La sonde | ✓ VERIFIED | 526 lignes. Implémente `IUsageProvider, IEtatServeur`. En-têtes lus avant aiguillage, frein 300s, interrupteur `SondeEnTetesActivee`, jeton demandé uniquement à `ChronosTokenAuthority`, corps HTTP jamais lu (`using (resp)` seul). |
| `src/Chronos/Models/StatutServeur.cs` | Vocabulaire ouvert | ✓ VERIFIED | Enum 4 membres + `StatutServeurTexte.DepuisEnTete` tolérant, jamais de repli sur Autorise. |
| `src/Chronos/Models/EtatDepassement.cs` | Fait de compte optionnel | ✓ VERIFIED | Tous champs optionnels, `EstRenseigne` évite de publier un dépassement vide. |
| `src/Chronos/Services/IEtatServeur.cs` | Canal latéral | ✓ VERIFIED | Contrat neutre (aucun type WPF), motif identique à IAuthStatus, événement sur transition uniquement. |
| `src/Chronos/Models/WindowState.cs` | 2 nouveaux champs | ✓ VERIFIED | `StatutServeur` et `Depassement` ajoutés en `init`, nullable, sans `required` — les 21 sites de construction préexistants compilent (confirmé par 652/652 tests verts). |
| `src/Chronos/Services/CompositeUsageProvider.cs` | INTACT | ✓ VERIFIED | `git log` ne montre aucun commit touchant ce fichier depuis le dernier commit de la phase 17 (8cb7b2d) ; instancié mais jamais édité, comme prévu (refonte = phase 19). |
| `src/Chronos/Models/UsageSnapshot.cs` | INTACT | ✓ VERIFIED | Même constat via `git log` — aucun commit de la phase 18. |
| `src/Chronos/Views/MainWindow.xaml` | INTACT | ✓ VERIFIED | `git diff 8cb7b2d..HEAD` = 0 changement. |
| `src/Chronos/App.xaml.cs` | Câblage DI | ✓ VERIFIED | Sonde en `primary` d'un nouveau `CompositeUsageProvider` externe, `IEtatServeur` aliasé sur la même instance (`sp.GetRequiredService<RateLimitHeaderUsageProvider>()`), `LastExactUsageProvider` reste le décorateur externe (tête de chaîne). |
| `src/Chronos/Services/DiagnosticService.cs` | Section sonde | ✓ VERIFIED | Nomme l'issue (`LibelleSonde`), les NOMS d'en-têtes (`NomsEnTetesRecus`, jamais une chaîne réseau brute), le statut serveur et le dépassement. |
| `src/Chronos/ViewModels/MainViewModel.cs` | Interrupteur + texte d'état | ✓ VERIFIED | `IsSondeEnTetesActivee`, `ToggleSondeEnTetesCommand`, `TexteEtatSonde`, `AfficherEtatSonde` tous présents et bindés. |
| `src/Chronos/ViewModels/WindowGaugeViewModel.cs` | Statut par fenêtre | ✓ VERIFIED | `TexteStatutServeur`/`HasStatutServeur`, absence de statut → `HasStatutServeur=false` → rien affiché (jamais « autorisé » par défaut). |
| `src/Chronos/Views/SettingsWindow.xaml` | Interrupteur + coût | ✓ VERIFIED | Binding `IsSondeEnTetesActivee`/`ToggleSondeEnTetesCommand`, libellé de coût explicite ≈288/jour, ligne d'état serveur/dépassement visible conditionnellement. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `RateLimitHeaderUsageProvider` | `ChronosTokenAuthority.GetAccessTokenAsync` | source unique de jeton | ✓ WIRED | Aucun autre appel de rafraîchissement dans le fichier ; `_autorite.GetAccessTokenAsync` est le seul point d'obtention du jeton. |
| `RateLimitHeaderUsageProvider` | `StatutServeurTexte.DepuisEnTete` | mapping unique du vocabulaire | ✓ WIRED | `LireStatut` et `LireDepassement` appellent tous deux `StatutServeurTexte.DepuisEnTete`, aucun switch dupliqué. |
| `RateLimitHeaderUsageProvider` | `WindowState.StatutServeur`/`Depassement` | posé par référence | ✓ WIRED | `LireFenetre` construit `WindowState { …, StatutServeur = statut, Depassement = depassement }`. |
| `App.xaml.cs` | `new CompositeUsageProvider(primary: sonde, …)` | composite externe instancié | ✓ WIRED | Ligne 339-348 : sonde en `primary`, ancienne chaîne en `fallback`, `CompositeUsageProvider.cs` non modifié. |
| `App.xaml.cs` | `IEtatServeur` | alias de la MÊME instance | ✓ WIRED | `services.AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>())` — prouvé par `Assert.Same` dans `CompositionRootTests`. |
| `SettingsWindow.xaml` | `MainViewModel.ToggleSondeEnTetesCommand` | Command binding | ✓ WIRED | `Command="{Binding ToggleSondeEnTetesCommand}"`, `IsChecked="{Binding IsSondeEnTetesActivee, Mode=OneWay}"`. |
| `DiagnosticService` | `IEtatServeur` | injection constructeur | ✓ WIRED | `DiagnosticService(..., IEtatServeur? etatServeur = null)`, section « sonde d'en-têtes » du rapport utilise `_etatServeur`. |

### Garde de position (falsifiabilité comportementale)

`CompositionRootTests.La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` construit deux sources
`Exact` produisant des chiffres différents (sonde = 0,01 ; `/api/oauth/usage` = 0,42) et assert que c'est
0,01 — et le `StatutServeur.Autorise` associé — qui sort du composite final. Une inversion primary/fallback
ferait ressortir 0,42 et ferait tomber ce test : garde falsifiable par construction, comme exigé. Vérifiée
par lecture du test (non mutée manuellement pour ne pas laisser de trace dans le dépôt de l'utilisateur,
conformément à la consigne — la preuve de falsifiabilité par mutation-puis-révocation a déjà été produite
et documentée par l'exécuteur dans 18-05-SUMMARY.md, §"Résultat de la mutation d'inversion primary/fallback").

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| HDR-01 | 18-03, 18-05 | Usage exact via en-têtes unified d'une requête jetable | ✓ SATISFIED | Voir Truth #1. |
| HDR-02 | 18-03, 18-05 | En-têtes exploités même sur 429 | ✓ SATISFIED (code) / ? NEEDS HUMAN (production) | Voir Truth #2/#2b. |
| HDR-03 | 18-02, 18-04, 18-06 | Statut serveur remonté au cadran | ✓ SATISFIED | Voir Truth #3. |
| HDR-04 | 18-02, 18-04, 18-06 | Usage en dépassement lu et affiché | ✓ SATISFIED | Voir Truth #3. |
| HDR-05 | 18-01 | Normalisation en un point unique | ✓ SATISFIED | Voir Truth #4. |
| HDR-06 | 18-03, 18-05, 18-06 | Cadence bornée, coût annoncé | ✓ SATISFIED | Voir Truth #5. |

Aucun requirement orphelin : les 6 IDs HDR-01..06 apparaissent tous dans les frontmatters `requirements:`
des 6 plans, et REQUIREMENTS.md ne mappe rien d'autre à la Phase 18.

### Contrôles supplémentaires imposés par le prompt

| Contrôle | Résultat |
|----------|----------|
| `CompositeUsageProvider.cs` intact | ✓ Aucun commit depuis 8cb7b2d (phase 17) |
| `UsageSnapshot.cs` intact | ✓ Aucun commit depuis 8cb7b2d (phase 17) |
| `MainWindow.xaml` intact | ✓ `git diff` vide depuis 8cb7b2d |
| `LastExactUsageProvider` en tête de chaîne | ✓ App.xaml.cs:339, `Assert.IsType<LastExactUsageProvider>` dans CompositionRootTests |
| Un seul rafraîchisseur (`RefreshAsync`) | ✓ Exactement 2 fichiers : `ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs` |
| `IEtatServeur` alias de la même instance | ✓ `Assert.Same(sonde, provider.GetRequiredService<IEtatServeur>())` |
| `anthropic-beta: oauth-2025-04-20` présent | ✓ Constante `Beta`, posée via `TryAddWithoutValidation("anthropic-beta", Beta)` |
| Aucun jeton logué/écrit/en exception/en URL ; corps HTTP jamais transporté | ✓ Jeton en variable locale uniquement ; `using (resp)` sans lecture de corps |
| `%APPDATA%\Chronos\oauth.dat` inchangé | ✓ `stat` confirme 518 octets, mtime 1783863147 — identique à la valeur attendue |
| Aucune requête réseau réelle en test ; aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` | ✓ Tous les tests examinés utilisent `FakeHttpMessageHandler` et `Path.GetTempPath()` |
| Pas d'anticipation phase 19 (doctrine composite) | ✓ Aucun `IsStale`/limite d'âge dans le code de la sonde ou des ViewModels |
| Pas d'anticipation phase 20 (frais/daté/indisponible, UniformGrid) | ✓ Diff `SettingsWindow.xaml` purement additif (24 lignes), aucune retouche de la grille existante |
| Aucun type WPF dans `Services/`/`Models/` | ✓ Les seuls fichiers avec `System.Windows.*` dans Services/ sont les adaptateurs de Phase 1 déjà exemptés nominativement par `ServicesLayerPurityTests` (`WpfUiDispatcher`, `OverlayController`, `TopmostGuard`) ; aucun fichier de la phase 18 n'en introduit |
| Suite complète stable (≥2 runs) | ✓ 652/652, 0 échec, exécutée 2 fois consécutivement (2m11s puis 2m5s) |

### Anti-Patterns Found

Aucun. Recherche de `TODO|FIXME|XXX|HACK|PLACEHOLDER|not implement|coming soon` sur les 6 fichiers
nouveaux/centraux de la phase (`RateLimitHeaderUsageProvider.cs`, `UsageNormalization.cs`,
`StatutServeur.cs`, `EtatDepassement.cs`, `IEtatServeur.cs`, `WindowState.cs`) : aucun résultat.
`EnsureSuccessStatusCode` absent de `Services/` — le piège explicitement redouté par HDR-02 n'existe pas.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète (652 tests attendus) | `dotnet test Chronos.sln -v q --nologo` (run 1) | 652 réussis, 0 échec, 2m11s | ✓ PASS |
| Stabilité (2e exécution) | `dotnet test Chronos.sln -v q --nologo` (run 2) | 652 réussis, 0 échec, 2m5s | ✓ PASS |
| Un seul rafraîchisseur | `grep -rln "RefreshAsync" src/Chronos --include="*.cs"` | 2 fichiers exacts | ✓ PASS |
| Coffre intact | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` | ✓ PASS |
| `CompositeUsageProvider`/`UsageSnapshot`/`MainWindow.xaml` intacts | `git log`/`git diff` depuis 8cb7b2d | Aucun changement | ✓ PASS |

### Human Verification Required

### 1. 429 réel avec en-têtes unified

**Test:** Après reconnexion OAuth avec un jeton valide, saturer volontairement la fenêtre 5 h (ou attendre
une saturation naturelle), puis ouvrir le menu Diagnostic de Chronos pendant que l'API répond 429.
**Expected:** Le diagnostic continue de nommer une issue `SaturationEnTetesLus`, affiche des chiffres
exacts pour les deux fenêtres et le statut serveur, au lieu de basculer sur « indisponible » ou de perdre
le dépassement.
**Why human:** Le jeton de la machine de développement est expiré depuis 2026-07-12, et aucune saturation
réelle n'est disponible pour un test automatisé. La famille d'en-têtes `anthropic-ratelimit-unified-*`
n'étant documentée nulle part publiquement, seule une réponse serveur réelle peut confirmer que les noms
d'en-têtes utilisés dans le code (`H5hUtil`, `H5hStatut`, etc.) correspondent à ce que le serveur envoie
réellement en production. C'est le seul point que la phase elle-même (18-06, 18-VALIDATION.md) désigne
comme checkpoint humain bloquant — HDR-02 est marqué « satisfait par le code » mais explicitement pas
« prouvé en production » tant que ce test n'a pas été fait.

### Gaps Summary

Aucun gap détecté. Les 6 requirements HDR-01 à HDR-06 sont satisfaits au niveau du code, avec des preuves
falsifiables (mutations mesurées puis révoquées, documentées dans les SUMMARY de chaque plan) et une suite
de 652 tests stable sur deux exécutions consécutives. Les fichiers protégés (`CompositeUsageProvider.cs`,
`UsageSnapshot.cs`, `MainWindow.xaml`) sont vérifiés intacts par l'historique git. La sonde est bien le
`primary` de la chaîne exacte, prouvé par un test de comportement falsifiable (inversion primary/fallback
ferait tomber le test). `IEtatServeur` est un alias de la même instance (`Assert.Same`), un seul
rafraîchisseur de jeton existe, le coffre réel de l'utilisateur est inchangé, et aucune anticipation des
phases 19/20 n'a été détectée.

Le seul point non tranchable par l'automatisation — un 429 réel portant effectivement les en-têtes
`anthropic-ratelimit-unified-*` en production — est un flag connu, documenté par la phase elle-même comme
checkpoint humain bloquant (pas un défaut de conception), d'où le statut `human_needed` plutôt que
`passed`. La dette de performance de `DiagnosticServiceTests` (~2m35 au lieu de 56s, cause : `BuildReportAsync`
non injectable) est documentée dans `deferred-items.md` comme candidate légitime de la phase 20 et n'est
pas traitée comme un gap, car elle ne touche à aucun des 5 critères de succès ni à aucune exigence HDR.

---

_Verified: 2026-09-12_
_Verifier: Claude (gsd-verifier)_
