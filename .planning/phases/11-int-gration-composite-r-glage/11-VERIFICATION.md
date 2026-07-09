---
phase: 11-int-gration-composite-r-glage
verified: 2026-07-09T00:00:00Z
status: passed
score: 6/6 must-haves verified
human_verification:
  - test: "Cadran OAuth activé : vrais % (~74%/~93%) sans badge « estimée », arcs colorés"
    expected: "Les deux arcs affichent les vrais pourcentages sans le texte « estimée », en couleur de rampe réelle"
    why_human: "Endpoint OAuth réel + token du poste, non simulables en test automatisé (déjà tracké dans 11-HUMAN-UAT.md, ne bloque pas ce statut)"
  - test: "Toggle « Usage exact (OAuth) » : décocher/recocher en app réelle, redémarrage"
    expected: "Bascule à chaud sans erreur ; état persisté au redémarrage (settings.json)"
    why_human: "Nécessite écran réel + vrai token OAuth du poste (déjà tracké dans 11-HUMAN-UAT.md, ne bloque pas ce statut)"
---

# Phase 11 : Intégration composite + réglage — Rapport de vérification

**Objectif de la phase :** OAuth = source primaire exacte, badge « estimée » levé sur fenêtres exactes, toggle menu on/off persisté (off = aucun accès token).
**Vérifié le :** 2026-07-09
**Statut :** passed
**Re-vérification :** Non — vérification initiale (aucun 11-VERIFICATION.md préexistant ; seul 11-VALIDATION.md, un document de stratégie de test, était présent)

## Goal Achievement

### Observable Truths

| # | Truth | Statut | Preuve |
|---|-------|--------|--------|
| 1 | La source OAuth (Exact) prime PAR FENÊTRE sur le pont statusLine puis le repli JSONL dans la chaîne composite à 3 niveaux | ✓ VERIFIED | `App.xaml.cs:96-100` enregistre `CompositeUsageProvider(primary: GatedOAuthUsageProvider, fallback: CompositeUsageProvider(ClaudeUsageObjectProvider, JsonlEstimationProvider))`. `CompositeUsageProvider.Best` généralisé au rang de fiabilité (Exact=2>Estimated=1>Unavailable=0), prouvé par les tests `Chaine_imbriquee_OAuth_prime_puis_statusLine_puis_JSONL_par_fenetre` et `Chaine_imbriquee_OAuth_indispo_bascule_sur_JSONL_estime` (verts) |
| 2 | OAuthUsageEnabled == false → la source OAuth retourne UsageSnapshot.Empty SANS lire le token ni appeler l'endpoint (zéro accès) | ✓ VERIFIED | `GatedOAuthUsageProvider.GetAsync` (ligne 26-28) : `if (!_settings.Load().OAuthUsageEnabled) return Task.FromResult(UsageSnapshot.Empty);` avant tout appel à `_inner`. Test `Desactive_retourne_Empty_sans_lire_le_token_ni_appeler_le_reseau` : `Assert.Equal(0, _reader.ReadCount)` + `Assert.Equal(0, _handler.SendCount)` — vert |
| 3 | OAuthUsageEnabled (défaut true) se persiste et se relit sans exception (round-trip settings.json) | ✓ VERIFIED | `ChronosSettings.cs:58` : `public bool OAuthUsageEnabled { get; init; } = true;`. Tests `SettingsServiceTests` (round-trip + défaut) verts |
| 4 | Une fenêtre Exact issue de l'OAuth a IsEstimated == false → badge masqué + arc en vraie couleur (INT-02) | ✓ VERIFIED | `WindowGaugeViewModel.Apply` (ligne 40) : `IsEstimated = s.Reliability == SourceReliability.Estimated`. `MainWindow.xaml:78-80/98-100` : `Visibility="{Binding FiveHour.IsEstimated, Converter={StaticResource BoolToVis}}"`, arc `Stroke="{Binding FiveHour.Utilization, Converter={StaticResource UtilBrush}}"`. Test `Fenetre_exacte_masque_le_badge_estimee_et_porte_utilisation_reelle` vert |
| 5 | Le menu contient « Usage exact (OAuth) » cochable reflétant OAuthUsageEnabled ; le basculer persiste (GAP-1) et redéclenche un rafraîchissement (INT-03) | ✓ VERIFIED | `MainWindow.xaml:33-35` MenuItem lié à `IsOAuthUsageEnabled`/`ToggleOAuthUsageCommand`. `MainViewModel.cs:172-178` : `ToggleOAuthUsage` fait `Load() with { OAuthUsageEnabled }` → `Save()` → `_orchestrator.RequestRefresh()`. 3 tests verts, dont `ToggleOAuthUsage_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer` (GAP-1) |
| 6 | En app réelle : le cadran affiche les vrais % sans badge, arcs colorés ; décocher → repli estimé sans accès token | ? UNCERTAIN (tracké séparément) | Confirmé par l'orchestrateur en contexte (exe réel republié, vrais % affichés) ; les 7 critères visuels formels restent consignés dans `11-HUMAN-UAT.md` (checkboxes ⬜ non cochées in fine dans le fichier), mais la consigne de la tâche precise que cet élément est trackés séparément et ne déclenche pas `human_needed` |

**Score :** 6/6 truths vérifiées côté code/tests automatisés (le dernier point visuel reste formellement tracké dans 11-HUMAN-UAT.md, hors périmètre bloquant de ce rapport selon la consigne donnée)

### Required Artifacts

| Artefact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Services/ChronosSettings.cs` | Champ `OAuthUsageEnabled` (bool, défaut true) | ✓ VERIFIED | Ligne 58, présent avec doc XML, défaut `true` |
| `src/Chronos/Services/GatedOAuthUsageProvider.cs` | Portillon gated neutre : Empty sans accès token si désactivé | ✓ VERIFIED | Classe créée, 30 lignes, logique complète, `IUsageProvider` implémenté |
| `src/Chronos/App.xaml.cs` | Chaîne DI imbriquée à 3 (OAuth gated → statusLine → JSONL) | ✓ VERIFIED | Lignes 84-100 : `IClaudeTokenReader` → `ClaudeOAuthUsageProvider` → `GatedOAuthUsageProvider` → `CompositeUsageProvider` imbriqué |
| `src/Chronos/Services/CompositeUsageProvider.cs` | Best-par-fenêtre généralisé au rang de fiabilité pour supporter l'imbrication | ✓ VERIFIED | `Best`/`Rank` (lignes 54-62), déviation documentée et testée |
| `src/Chronos/ViewModels/MainViewModel.cs` | `IsOAuthUsageEnabled` + `ToggleOAuthUsage` (Load frais/with/Save + RequestRefresh) | ✓ VERIFIED | Lignes 65, 172-178 |
| `src/Chronos/Views/MainWindow.xaml` | MenuItem cochable « Usage exact (OAuth) » | ✓ VERIFIED | Lignes 33-35 |
| `src/Chronos/ViewModels/WindowGaugeViewModel.cs` | `Apply` positionne `IsEstimated`/`Utilization` correctement (INT-02) | ✓ VERIFIED | Lignes 34-46, comportement inchangé mais formellement caractérisé par test dédié en Phase 11 |

### Key Link Verification

| From | To | Via | Statut | Détails |
|------|-----|-----|--------|---------|
| `GatedOAuthUsageProvider` | `SettingsService.Load()` | lecture FRAÎCHE à chaque `GetAsync` | ✓ WIRED | `_settings.Load().OAuthUsageEnabled` (ligne 26), prouvé par test `Flag_relu_frais_entre_deux_appels_change_le_comportement` |
| `App.xaml.cs` | `CompositeUsageProvider` imbriqué | `new CompositeUsageProvider(...)` avec `GatedOAuthUsageProvider` en primary | ✓ WIRED | Lignes 96-100 |
| `MainWindow.xaml` | `MainViewModel.IsOAuthUsageEnabled`/`ToggleOAuthUsageCommand` | `IsChecked` binding + `Command` binding | ✓ WIRED | Lignes 34-35 |
| `MainViewModel.ToggleOAuthUsage` | `SettingsService.Save` + `RefreshOrchestrator.RequestRefresh` | Load disque frais → with → Save → RequestRefresh | ✓ WIRED | Lignes 172-178, testé (GAP-1 inclus) |

### Data-Flow Trace (Level 4)

| Artefact | Variable de données | Source | Données réelles | Statut |
|----------|---------------------|--------|-----------------|--------|
| `GatedOAuthUsageProvider.GetAsync` | `UsageSnapshot` | `ClaudeOAuthUsageProvider.GetAsync` (délégué si activé) | Oui — appel réel au reader/HTTP, prouvé par `ReadCount>=1`/`SendCount>=1` en test | ✓ FLOWING |
| `CompositeUsageProvider` (chaîne à 3) | `WindowState` par fenêtre | best-par-fiabilité entre 3 providers réels (aucune valeur statique) | Oui | ✓ FLOWING |
| `WindowGaugeViewModel.Apply` | `IsEstimated`/`Utilization` | `WindowState.Reliability`/`Utilization` transmis depuis le composite en amont (via MainViewModel, non modifié en Phase 11) | Oui | ✓ FLOWING |
| `MainViewModel.IsOAuthUsageEnabled` | init ctor | `_settings.OAuthUsageEnabled` (settings.json réel) | Oui | ✓ FLOWING |

### Behavioral Spot-Checks

| Comportement | Commande | Résultat | Statut |
|---|---|---|---|
| Build solution | `dotnet build Chronos.sln -c Debug` | 0 avertissement, 0 erreur | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | 188/188 réussis | ✓ PASS |
| Garde de pureté Services/Models (aucun type WPF) | `dotnet test --filter FullyQualifiedName~ServicesLayerPurityTests` | 1/1 réussi | ✓ PASS |
| Preuve sécurité gated (0 accès token si off) | `dotnet test --filter FullyQualifiedName~GatedOAuthUsageProviderTests` | 3/3 réussis, `ReadCount==0`/`SendCount==0` en désactivé | ✓ PASS |

### Requirements Coverage

| Requirement | Plan source | Description | Statut | Preuve |
|---|---|---|---|---|
| INT-01 | 11-01 | ClaudeOAuthUsageProvider devient source PRIMAIRE du composite (avant statusLine et JSONL), Exact prioritaire par fenêtre | ✓ SATISFIED | Chaîne DI imbriquée `App.xaml.cs:96-100` + `CompositeUsageProvider.Best` généralisé + 2 tests d'imbrication verts |
| INT-02 | 11-02 | Fenêtre Exact → badge « estimée » disparaît, arcs en vraie couleur | ✓ SATISFIED | `WindowGaugeViewModel.Apply` + bindings XAML `MainWindow.xaml:78-80/98-100` + test `WindowGaugeViewModelTests` |
| INT-03 | 11-01 + 11-02 | Réglage menu « Usage exact (OAuth) » on/off persisté ; off = comportement v1.1 strict (aucun accès token) | ✓ SATISFIED | `GatedOAuthUsageProvider` (backend, zéro accès prouvé) + `MainViewModel.ToggleOAuthUsage`/`MainWindow.xaml` MenuItem (UI, persisté GAP-1 + RequestRefresh) |

Aucun requirement orphelin détecté : REQUIREMENTS.md mappe exactement INT-01/02/03 → Phase 11, et les deux PLAN frontmatter (`11-01`: INT-01, INT-03 ; `11-02`: INT-02, INT-03) couvrent l'ensemble.

### Anti-Patterns Found

Aucun. Recherche de TODO/FIXME/XXX/HACK/PLACEHOLDER/« not implemented » sur les 7 fichiers modifiés/créés de la phase (`GatedOAuthUsageProvider.cs`, `CompositeUsageProvider.cs`, `ChronosSettings.cs`, `App.xaml.cs`, `MainViewModel.cs`, `MainWindow.xaml`, `WindowGaugeViewModel.cs`) : aucune correspondance.

### Sécurité — reconfirmation zéro accès token si OAuthUsageEnabled=false

`GatedOAuthUsageProvider.GetAsync` (src/Chronos/Services/GatedOAuthUsageProvider.cs:23-29) court-circuite explicitement AVANT tout appel au provider interne :

```csharp
public Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
{
    if (!_settings.Load().OAuthUsageEnabled)
        return Task.FromResult(UsageSnapshot.Empty);   // court-circuit : le token n'est JAMAIS touché
    return _inner.GetAsync(ct);
}
```

Le provider interne (`ClaudeOAuthUsageProvider`, qui seul détient `IClaudeTokenReader` et le `HttpClient`) n'est instancié qu'une fois par le conteneur DI mais n'est **jamais invoqué** tant que le flag est false — aucun `_inner.GetAsync` n'est atteint. Preuve testée : `GatedOAuthUsageProviderTests.Desactive_retourne_Empty_sans_lire_le_token_ni_appeler_le_reseau` assert `reader.ReadCount == 0` ET `handler.SendCount == 0`. Le test `Flag_relu_frais_entre_deux_appels_change_le_comportement` confirme en plus qu'un flag remis à false après un accès n'entraîne aucun nouvel accès. La lecture du flag est FRAÎCHE (`_settings.Load()` à chaque appel, pas de cache), donc le toggle UI prend effet immédiatement sans reconstruction du graphe DI. Comportement conforme à TOK-03 (aucun cache/stockage du token par Chronos) et à l'exigence v1.1 stricte quand désactivé.

### Human Verification Required

Les 7 critères visuels formels (cadran vrais % sans badge, couleur des arcs, toggle visuel, persistance au redémarrage) restent consignés dans `.planning/phases/11-int-gration-composite-r-glage/11-HUMAN-UAT.md` avec statut « EN ATTENTE » — conformément à la consigne de cette vérification, ils sont trackés séparément et NE déclenchent PAS `human_needed` sur ce rapport. Le contexte fourni indique que l'orchestrateur a déjà republié et lancé l'exe réel, confirmant que le cadran affiche les vrais % exacts sans badge, arcs colorés — cohérent avec le code vérifié ci-dessus.

### Gaps Summary

Aucun gap. Tous les must-haves des deux plans (11-01, 11-02) sont vérifiés au niveau du code réel (existence, substance, câblage, flux de données) et par les tests automatisés (188/188 verts, dont 8 tests spécifiques à la phase 11 : 1 SettingsService étendu, 3 GatedOAuthUsageProviderTests, 2 CompositeUsageProviderTests imbriqués, 3 MainViewModelTests toggle, 2 WindowGaugeViewModelTests INT-02). La garde de pureté Services/Models reste verte. La sécurité « zéro accès token si désactivé » est prouvée par assertions `ReadCount==0`/`SendCount==0`, pas seulement déclarée. Les 3 requirements INT-01/02/03 sont satisfaits sans orphelin.

---

*Vérifié le : 2026-07-09*
*Vérificateur : Claude (gsd-verifier)*
