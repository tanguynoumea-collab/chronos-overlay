---
phase: 40
slug: cadrans-l-chelle-1-et-orientations
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-10-03
---

# Phase 40 — Validation Strategy

> Contrat de validation par phase : échantillonnage du retour pendant l'exécution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, collection `"XAML WPF"`) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~EmpreinteCadran\|FullyQualifiedName~GeometrieCadrans\|FullyQualifiedName~CornerSnap\|FullyQualifiedName~ControlesCadransOrientation\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~OrientationCadran\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~OverlayController\|FullyQualifiedName~GardeGestesCadran\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~GardeTokens"` |
| **Full suite command** | `dotnet build Chronos.sln` (0 avertissement, Debug et Release) puis `dotnet test Chronos.sln` |
| **Estimated runtime** | ~60 s (filtré) ; ~4-6 min (suite complète, ~1800 tests) |

---

## Sampling Rate

- **Après chaque commit de tâche :** la commande `<automated>` de la tâche (filtrée).
- **Après chaque plan :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` (suite complète).
- **Avant `/gsd:verify-work` :** build 0 avertissement (Debug + Release) et suite complète verte.
- **Latence max du retour :** ~60 s par tâche.
- Exécution SÉQUENTIELLE sur un seul arbre (pas de worktree) : chaque tâche RED casse volontairement la compilation du projet de test
  jusqu'à la tâche GREEN suivante du même plan ; aucun plan ne démarre tant que le précédent n'est pas vert.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 40-01-01 | 01 | 1 | CAD-01, CAD-04 | unit (RED) | `dotnet build tests/Chronos.Tests/Chronos.Tests.csproj` (échec attendu sur EmpreinteCadran / OrientationCadran) | ❌ W0 `EmpreinteCadranTests.cs` | ⬜ pending |
| 40-01-02 | 01 | 1 | CAD-01, CAD-04 | unit | `--filter "FullyQualifiedName~EmpreinteCadran\|FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~GardeTokensHistorique\|FullyQualifiedName~GardeTokensReglages\|FullyQualifiedName~ServicesLayerPurity"` | ✅ après 01-01 | ⬜ pending |
| 40-02-01 | 02 | 2 | CAD-02, CAD-03 | unit (RED) | build en échec sur GeometrieCadrans / RecalerSurCoin | ❌ W0 `GeometrieCadransTests.cs` | ⬜ pending |
| 40-02-02 | 02 | 2 | CAD-02, CAD-03 | unit | `--filter "FullyQualifiedName~GeometrieCadrans\|FullyQualifiedName~CornerSnap"` | ✅ après 02-01 | ⬜ pending |
| 40-03-01 | 03 | 3 | CAD-03 | WPF rendu (RED) | build en échec sur `Orientation` des contrôles | ❌ W0 `ControlesCadransOrientationTests.cs` | ⬜ pending |
| 40-03-02 | 03 | 3 | CAD-03 | WPF rendu | `--filter "FullyQualifiedName~ControlesCadransOrientation\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadranBindingTests"` | ✅ après 03-01 | ⬜ pending |
| 40-04-01 | 04 | 4 | CAD-01, CAD-03 | WPF (RED) | build en échec sur `Orientation` des vues | ❌ W0 `CadransOrientationBindingTests.cs` | ⬜ pending |
| 40-04-02 | 04 | 4 | CAD-01, CAD-03 | WPF | `--filter "FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadranBindingTests"` | ✅ après 04-01 | ⬜ pending |
| 40-05-01 | 05 | 5 | CAD-01, CAD-03 | WPF (RED → GREEN) | `--filter "FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~GardeCouleursCadrans"` | ✅ | ⬜ pending |
| 40-05-02 | 05 | 5 | CAD-01 | WPF + garde | `--filter "FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~GardeGestesCadran\|FullyQualifiedName~ReglagesWindowTests"` | ✅ | ⬜ pending |
| 40-06-01 | 06 | 6 | CAD-04, CAD-01 | unit (RED → GREEN) | `--filter "FullyQualifiedName~OrientationCadranTests\|FullyQualifiedName~MainViewModelTests"` | ❌ W0 `OrientationCadranTests.cs` | ⬜ pending |
| 40-06-02 | 06 | 6 | CAD-01, CAD-03 | WPF + garde textuelle | `--filter "FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~EmpreinteCadran\|FullyQualifiedName~GardeGestesCadran\|FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~OverlayWindowConfig"` | ✅ | ⬜ pending |
| 40-07-01 | 07 | 7 | CAD-02 | WPF (RED) | build en échec sur RecalerSurCoinCourant / CoinCourant / SWP_NOZORDER | ✅ (extension) | ⬜ pending |
| 40-07-02 | 07 | 7 | CAD-02 | WPF + garde textuelle | `--filter "FullyQualifiedName~OverlayController\|FullyQualifiedName~GardeGestesCadran\|FullyQualifiedName~CornerSnap\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~ServicesLayerPurity\|FullyQualifiedName~TopmostGuard"` | ✅ | ⬜ pending |
| 40-08-01 | 08 | 8 | CAD-04, CAD-02 | WPF (RED → GREEN) | `--filter "FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~GardeTokensReglages\|FullyQualifiedName~ReglagesBindingTests"` | ✅ (extension) | ⬜ pending |
| 40-08-02 | 08 | 8 | CAD-04 | WPF (RED → GREEN) | `--filter "FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadransThemeBinding"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Couverture des critères de succès (ROADMAP Phase 40)

| Critère | Preuve automatisée |
|---------|--------------------|
| 1. Chaque cadran à sa vraie taille (plus de Viewbox, `EmpreinteCadran` depuis les tokens, fenêtre à l'empreinte, corps 11/12/13/14) | `EmpreinteCadranTests` (pur + miroir XDocument + garde « pas de `<Viewbox` » dans MainWindow), `CadranBindingTests.La_fenetre_prend_l_empreinte_de_chaque_variante`, `CadransOrientationBindingTests` (empreinte exacte, corps) |
| 2. La fenêtre tient son coin (4 coins, 100 % / 150 %, multi-écrans) ; aperçu à l'empreinte réelle | `CornerSnapTests.Le_recalage_garde_le_coin_et_reste_dans_la_zone` (origine négative, 150 %), `OverlayControllerTests` (coin courant, `SWP_NOZORDER`, aucune persistance), `CadranBindingTests.Au_demarrage_la_restauration_utilise_l_empreinte_du_style_persiste` (Fusible H au coin bas-droite : 190 × 92, jamais 0), `GardeGestesCadranTests` (SizeChanged + garde de drag), `ReglagesWindowTests` (Rect 190×66 puis 110×190) |
| 3. Trois variantes nouvelles reconnaissables + états attente / plancher / indisponible | `GeometrieCadransTests` (sens, ondulation, volets), `ControlesCadransOrientationTests` (rendu des deux sens et des états), `CadransOrientationBindingTests` (textes au pire cas, gabarits), `CadranBindingTests.Le_mot_et_les_pastilles_restent_dans_l_empreinte_de_chaque_variante` |
| 4. Orientation par cadran, carte visible pour 3 styles, indépendante de « Disposition verticale », tolérance, galerie 8 variantes | `OrientationCadranTests`, `SettingsServiceTests` (Marée 99 → Vertical + générique), `ReglagesWindowTests` (carte), `CadransOrientationBindingTests.La_galerie_montre_les_huit_variantes` |

---

## Wave 0 Requirements

Créés par les tâches RED de chaque plan (avant le code qu'ils prouvent) :

- [ ] `tests/Chronos.Tests/EmpreinteCadranTests.cs` — CAD-01 (plan 01 ; garde « pas de Viewbox » ajoutée au plan 06)
- [ ] `tests/Chronos.Tests/GeometrieCadransTests.cs` — CAD-03 (plan 02)
- [ ] `tests/Chronos.Tests/ControlesCadransOrientationTests.cs` — CAD-03 (plan 03)
- [ ] `tests/Chronos.Tests/CadransOrientationBindingTests.cs` — CAD-01/03/04 (plan 04, étendu aux plans 05 et 08)
- [ ] `tests/Chronos.Tests/OrientationCadranTests.cs` — CAD-04 (plan 06)
- [ ] Extensions : `CornerSnapTests` (02), `SettingsServiceTests` + `GardeTokensHistoriqueTests` (01), `CadransThemeBindingTests` (04, 05),
      `GardeCouleursCadransTests` 4 → 5 vues + `CadranBindingTests` `ArcNomme` (05), `CadranBindingTests` 8 variantes (06),
      `OverlayControllerTests` + `GardeGestesCadranTests` (07), `ReglagesWindowTests` (08)

Infrastructure existante (xUnit + StaFact, collection `"XAML WPF"`, `GardesPerimetreTests.CheminSources()`) : aucune installation.

---

## Manual-Only Verifications

L'agent ne lance, n'arrête ni ne clique JAMAIS l'overlay : ces vérifications sont faites par l'utilisateur (revue visuelle du cycle
ZEUS / constat de la phase 43).

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Pas de débordement après changement de style ou d'orientation, 8 variantes × 4 coins × 100 % / 150 %, multi-écrans à DPI mixte | CAD-02 | Ordre WPF exact (redimensionnement HWND → `SizeChanged`) raisonné, non instrumenté ; second écran non disponible à l'agent | Lancer l'exe ; accrocher le cadran à chaque coin ; dans Réglages › Apparence, passer Anneaux → Fusible → Fusible vertical → Marée horizontale → Volets vertical ; la fenêtre reste au coin, entièrement visible ; répéter sur un écran à 150 % et après un glisser entre écrans |
| Saut d'une image au redimensionnement (fenêtre agrandie vers la droite/bas puis recalée) jugé acceptable | CAD-02 | Perception visuelle | Observer la bascule de style au coin bas-droite |
| Lisibilité et reconnaissance des 8 variantes (Volets vertical en particulier — §8 du plan de design : retrait possible sur décision utilisateur) | CAD-03 | Jugement visuel | `Chronos.exe --cadrans` : comparer à `.zeus/maquettes/cycle2-cadrans-themes.html` § 1, thèmes Minuit / Nord / Néon, curseurs temps / quota / estimé |
| Fenêtre étroite (110 DIP) et basse (66 DIP) réellement à cette taille | CAD-01 | Taille minimale imposée par Windows non vérifiable sans affichage (Pitfall 8) | Fusible vertical / Volets horizontal : la fenêtre ne dépasse pas l'empreinte (aucun cadre vide autour) |

---

## Validation Sign-Off

- [x] Toutes les tâches ont une commande `<automated>` (les tâches RED vérifient l'échec de compilation attendu)
- [x] Continuité d'échantillonnage : jamais 3 tâches consécutives sans vérification automatisée
- [x] Wave 0 couvre toutes les références de tests nouveaux
- [x] Aucun mode watch
- [x] Latence du retour < 60 s par tâche (hors suite complète)
- [x] `nyquist_compliant: true` dans le frontmatter

**Approval:** pending
