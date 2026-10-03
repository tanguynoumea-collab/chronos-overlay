---
phase: 42
slug: un-geste-sur-toute-la-silhouette
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-10-03
---

# Phase 42 — Validation Strategy

> Contrat de validation par phase : échantillonnage du retour pendant l'exécution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, collection `"XAML WPF"`) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemin des sources injecté : `CheminSourcesChronos`) |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~AutomateGeste\|FullyQualifiedName~ZonesGeste\|FullyQualifiedName~GardeGestesCadran\|FullyQualifiedName~ArbitreClicCentre\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~ReglagesBindingTests\|FullyQualifiedName~GardeDocumentationHistorique"` |
| **Full suite command** | `dotnet build Chronos.sln` (0 avertissement, Debug et Release) puis `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Estimated runtime** | ~60 s (filtré) ; ~4-6 min (suite complète, ~1870 tests) |

---

## Sampling Rate

- **Après chaque commit de tâche :** la commande `<automated>` de la tâche (filtrée).
- **Après chaque plan :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` (suite complète).
- **Avant `/gsd:verify-work` :** build 0 avertissement (Debug + Release) et suite complète verte.
- **Latence max du retour :** ~60 s par tâche.
- Exécution SÉQUENTIELLE sur un seul arbre (pas de worktree) : les tâches RED des plans 01 et 02 cassent volontairement la compilation du
  projet de test jusqu'à la tâche GREEN suivante du même plan ; les RED des plans 03 et 04 compilent et échouent par assertion. Aucun plan
  ne démarre tant que le précédent n'est pas vert. La phase 41 (Braises) est exécutée AVANT : ses fichiers sont lus tels que livrés.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 42-01-01 | 01 | 1 | GST-01 | unit pur (RED) | `dotnet build tests/Chronos.Tests/Chronos.Tests.csproj` (échec attendu sur AutomateGeste / ActionGeste / EtatGeste) | ❌ W0 `AutomateGesteTests.cs` | ⬜ pending |
| 42-01-02 | 01 | 1 | GST-01 | unit pur | `--filter "FullyQualifiedName~AutomateGesteTests\|FullyQualifiedName~ArbitreClicCentreTests\|FullyQualifiedName~GardeGestesCadranTests"` | ✅ après 01-01 | ⬜ pending |
| 42-02-01 | 02 | 2 | GST-02, GST-03 | WPF rendu (RED) | build en échec sur `ZoneGeste` | ❌ W0 `ZonesGesteRenduTests.cs` | ⬜ pending |
| 42-02-02 | 02 | 2 | GST-02, GST-03 | WPF rendu + garde texte | `--filter "FullyQualifiedName~ZonesGesteRenduTests\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~EmpreinteCadran"` | ✅ après 02-01 | ⬜ pending |
| 42-03-01 | 03 | 3 | GST-01, GST-02, GST-03 | texte + WPF (RED par assertion) | `--filter "FullyQualifiedName~GardeGestesCadranTests\|FullyQualifiedName~ZonesGesteRenduTests"` (nouveaux tests rouges, plan 02 vert) | ✅ (réécriture + extension) | ⬜ pending |
| 42-03-02 | 03 | 3 | GST-01, GST-02, GST-03 | texte + WPF | `--filter "FullyQualifiedName~GardeGestesCadranTests\|FullyQualifiedName~ZonesGesteRenduTests\|FullyQualifiedName~AutomateGesteTests\|FullyQualifiedName~ArbitreClicCentreTests\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~OverlayController\|FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~ReglagesWindowTests"` | ✅ | ⬜ pending |
| 42-04-01 | 04 | 4 | GST-01 | texte + WPF (RED par assertion) | `--filter "FullyQualifiedName~ReglagesBindingTests\|FullyQualifiedName~GardeDocumentationHistorique\|FullyQualifiedName~GardeGestesCadranTests"` | ✅ (extension) | ⬜ pending |
| 42-04-02 | 04 | 4 | GST-01 | texte + WPF | même filtre + `FullyQualifiedName~ReglagesWindowTests` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Couverture des critères de succès (ROADMAP Phase 42)

| Critère | Preuve automatisée |
|---------|--------------------|
| 1. Toute la silhouette répond de la même façon (clic, double-clic, glisser au seuil, clic droit) ; `CentreHit` supprimé ; pastilles gardent leurs clics ; un seul répartiteur | `AutomateGesteTests` (13 cas : seuil strict par axe, réentrance de `DragMove`, double-clic à l'appui, perte de capture, double compté après glisser), `ArbitreClicCentreTests` (inchangé), `GardeGestesCadranTests` (répartiteur sur `Racine`, filtre `ZoneGeste.Contient`, `ClickCount` jamais lu au relâchement, `DragMove` puis `SnapToNearestCorner`, plus de `CentreHit` ni de `MouseLeftButtonDown +=`), `ZonesGesteRenduTests.Le_clic_droit_ouvre_les_reglages_partout_ou_il_arrive`, `ZonesGesteRenduTests.Une_pastille_visible_garde_son_propre_clic` |
| 2. Hors silhouette, le bureau reçoit le clic ; `ZoneSilhouette` = `#01000000` ; garde « aucun pinceau nul ou Transparent » | `ZonesGesteRenduTests.Le_rendu_capte_dans_la_silhouette_et_laisse_passer_dehors` (alpha = 0 aux témoins dehors), `…Les_vues_de_cadran_n_ont_ni_fond_Transparent_ni_alpha_litteral`, `…Aucun_element_a_geste_n_a_de_pinceau_nul_ou_transparent` (runtime : silhouettes, ButtonBase + gabarit, porteurs d'infobulle), `…La_pastille_d_age_capte_sur_tout_son_disque`, `GardeGestesCadranTests.Les_commentaires_faux_ont_disparu` (`"Transparent"` unique sur la Window) |
| 3. Prouvé pour les huit variantes (rendu + HitTest, jamais HitTest seul) ; commentaires corrigés ; `GardeGestesCadranTests` adapté | `ZonesGesteRenduTests` : déclaration (1 silhouette, contrat de forme), rendu `RenderTargetBitmap` nominal + indisponible, `Le_HitTest_route_dedans_vers_la_racine_et_rien_dehors` ; `GardeGestesCadranTests` réécrit |
| Mots (CONTEXT) : « Aussi : double-clic sur le cadran », README / note 3.5.0 | `ReglagesBindingTests` (2 assertions), `GardeDocumentationHistoriqueTests`, `GardeGestesCadranTests.La_documentation_des_gestes_suit_le_geste_unique` |

### Points témoins (contrat, générés dans le test, jamais lus dans le XAML)

- **Disque** (Arcs, Braises ; 170 × 170, centre 85,85, r 74) — dedans : (85,85) (15,85) (155,85) (85,15) (85,155) (35,35) (134,35) (35,134)
  (134,134) (85,40) ; dehors : (2,2) (167,2) (2,167) (167,167) (6,85) (164,85) (85,164) (40,3) (130,3). Aucun témoin dehors dans le secteur
  de la flèche de Braises `x ∈ [76,94], y ∈ [0,26]` (phase 41 : la flèche dépasse le disque, assumé).
- **Rectangle arrondi** (Fusible H 190 × 92 / V 110 × 190, Marée V 132 × 160 / H 190 × 96, Volets H 190 × 66 / V 128 × 190 ; r 10) — dedans :
  (W/2,H/2) (3,H/2) (W−4,H/2) (W/2,3) (W/2,H−4) (5,5) (W−6,5) (5,H−6) (W−6,H−6) ; dehors : (0,0) (W−1,0) (0,H−1) (W−1,H−1).
- Rendu à 96 DPI (1 px = 1 DIP), témoins à ≥ 2 DIP de toute frontière ; non-vacuité vérifiée (taille de la racine = empreinte).

---

## Wave 0 Requirements

Créés par les tâches RED de chaque plan (avant le code qu'ils prouvent) :

- [ ] `tests/Chronos.Tests/AutomateGesteTests.cs` — GST-01 (plan 01, pur, sans `[Collection]`)
- [ ] `tests/Chronos.Tests/ZonesGesteRenduTests.cs` — GST-02 / GST-03 (plan 02, `[Collection("XAML WPF")]`, étendu au plan 03)
- [ ] Réécriture de `tests/Chronos.Tests/GardeGestesCadranTests.cs` (plan 03, étendu au plan 04)

Aucun paquet à installer.

---

## Manual-Only Verifications

L'agent ne lance, n'arrête ni ne clique JAMAIS l'overlay (STATE.md). Ces vérifications font partie du constat utilisateur (phase 43, VAL-07).

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Clic sur un pixel « vide » de la silhouette (entre deux anneaux d'Arcs, interstice de Volets) → bascule % ↔ temps ≈ 0,5 s après le relâchement | GST-01 | Le hit-test de l'OS sur fenêtre layered et `ClickCount` réel ne se simulent pas | Pour chacune des 8 variantes (Réglages → Apparence → style + orientation) : cliquer sans bouger hors des chiffres, dans la silhouette |
| Double-clic n'importe où sur la silhouette → Historique, sans bascule visible | GST-01 | idem | Double-clic au bord du disque / dans un coin du rectangle ; le centre ne doit pas changer de mode |
| Appuyer puis glisser → la fenêtre suit, puis s'accroche au coin le plus proche ; aucune bascule après le relâchement | GST-01 | `DragMove` est une boucle modale système | Glisser vers chaque coin, à 100 % et 150 % (et entre deux écrans si disponible) |
| Clic droit sur la silhouette ET sur une pastille → Réglages | GST-01 | idem | Clic droit au bord du cadran, puis sur la pastille d'âge si visible |
| Clic à côté du cadran (coin hors disque, au-delà du rectangle) → l'élément du bureau dessous reçoit le clic | GST-02 | Seul Windows décide (alpha 0) | Poser l'overlay sur une icône du bureau, cliquer dans le coin vide de l'empreinte d'Arcs |
| Les pastilles (déconnexion, invitation) lancent toujours la reconnexion ; l'infobulle de la pastille d'âge s'ouvre sur tout son disque | GST-01, GST-02 | Interaction réelle | Survoler / cliquer les pastilles quand elles sont visibles |
| Aucune trace visible de la silhouette (`#01000000` imperceptible) sur chaque thème | GST-02 | Jugement visuel | Regarder chaque variante devant un fond d'écran clair puis sombre |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
