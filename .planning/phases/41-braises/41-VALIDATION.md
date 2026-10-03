---
phase: 41
slug: braises
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-10-03
---

# Phase 41 — Validation Strategy

> Contrat de validation par phase : échantillonnage du retour pendant l'exécution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact (`[WpfFact]`, collection `"XAML WPF"`) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~BraisesGeometrie\|FullyQualifiedName~CadranBraises\|FullyQualifiedName~WindowGaugeViewModelTests\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~GardesDoctrine"` |
| **Full suite command** | `dotnet build Chronos.sln` (0 avertissement, Debug et Release) puis `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Estimated runtime** | ~40 s (filtré) ; ~4-6 min (suite complète, ~1870 tests) |

---

## Sampling Rate

- **Après chaque commit de tâche :** la commande `<automated>` de la tâche (filtrée).
- **Après chaque plan :** suite complète.
- **Avant `/gsd:verify-work` :** build 0 avertissement (Debug + Release) et suite complète verte.
- **Latence max du retour :** ~60 s par tâche.
- Exécution SÉQUENTIELLE sur un seul arbre (pas de worktree, pas de vagues parallèles) : chaque commit RED peut casser la compilation du
  projet de test jusqu'au commit GREEN de la même tâche ; 41-02 ne démarre qu'avec 41-01 vert. La phase 42 (silhouette, touche aussi
  `CadranBraisesView`) s'exécute APRÈS la 41.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 41-01-01 | 01 | 1 | BRA-01 | unit pur + rendu (TDD) | `--filter "FullyQualifiedName~BraisesGeometrie\|FullyQualifiedName~CadranBraisesTests\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~ControlesCadransOrientation"` | ❌ W0 `BraisesGeometrieTests.cs`, `CadranBraisesTests.cs` (créés en RED) | ⬜ pending |
| 41-01-02 | 01 | 1 | BRA-01 | liaison WPF (TDD) | `--filter "FullyQualifiedName~CadranBraises\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadranBindingTests"` | ✅ après 01-01 | ⬜ pending |
| 41-02-01 | 02 | 2 | BRA-02 | unit (TDD) | `--filter "FullyQualifiedName~WindowGaugeViewModelTests\|FullyQualifiedName~GardesDoctrine\|FullyQualifiedName~MainViewModel"` | ✅ fichier existant, tests ajoutés | ⬜ pending |
| 41-02-02 | 02 | 2 | BRA-02 | liaison WPF (TDD) + suite | `--filter "FullyQualifiedName~CadranBraises\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~WindowGaugeViewModelTests"` puis suite complète | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Comportement → preuve

| Comportement (critère de succès ROADMAP) | Preuve automatisée |
|------------------------------------------|--------------------|
| 20 braises / 5 groupes de 4, 72° par groupe, pas 13,2°, vide 32,4°, midi au milieu d'un vide | `BraisesGeometrieTests` (16,2 / 29,4 / 55,8 / 88,2 / 343,8 ; écarts ; symétrie) |
| Rendu groupé réel, attente aux mêmes angles, allumées = FractionRemaining | `CadranBraisesTests` (RenderTargetBitmap : braise à 16,2°, rien à 0° et 72°, Fraction 0,5 → 10 allumées) |
| Hebdo inchangé (12, R44, 3,6, uniforme) | `BraisesGeometrieTests.Anneau_hebdo…`, rendu à 30°, `CadransThemeBindingTests` (GroupSize 1) |
| Flèche fixe à midi, triangle 10 × 7 + filet 12, `TickReset` du thème, non cliquable, sans animation | `CadransThemeBindingTests` (15 thèmes, `Assert.Same`), `CadranBraisesTests` (points, hit-test), grep `Storyboard` = 0 |
| « ↻ HH:MM » exact en mode temps, absent si inconnu / atteint, plancher conservé, DST | `WindowGaugeViewModelTests` (fuseau Paris) |
| Mode temps : 3e ligne ; mode % : centre inchangé ; MainWindow réelle | `CadranBraisesTests` (galerie), `CadranBindingTests` (MainViewModel + snapshot) |
| Aucune couleur littérale, comptes 5 vues / 4 contrôles inchangés | `GardeCouleursCadransTests` (non modifiée) |
| Aucun seuil d'âge dans la VM | `GardesDoctrineTests` |

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/BraisesGeometrieTests.cs` — BRA-01 (pur), créé au RED de 41-01-01
- [ ] `tests/Chronos.Tests/CadranBraisesTests.cs` — BRA-01 rendu / flèche, BRA-02 ligne (STA, `[Collection("XAML WPF")]`), créé au RED de 41-01-01
- [ ] Ajouts dans `tests/Chronos.Tests/WindowGaugeViewModelTests.cs`, `CadransThemeBindingTests.cs`, `CadranBindingTests.cs`

Infrastructure de test existante : aucun paquet à ajouter.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Lisibilité des 5 groupes (le vide se lit comme une délimitation) et de la flèche à midi | BRA-01 | jugement visuel (§ 9.3 du plan de design) | L'utilisateur lance `Chronos.exe --cadrans`, tuile Braises, curseur temps 5 h de 0 à 100 % : la dernière braise allumée recule vers midi ; 15 thèmes ; échelle Windows 100 % puis 150 % |
| « ↻ HH:MM » sous les deux comptes à rebours, centré, non tronqué | BRA-02 | jugement visuel | Galerie : bascule « temps » ; puis overlay réel en style Braises, clic au centre : l'heure correspond à `/usage` |
| Flèche ni floue ni coupée au bord de l'empreinte | BRA-01 | rendu DPI réel | Overlay réel à 150 % : triangle entier visible en haut du cadran |

L'agent ne lance, n'arrête ni ne clique jamais l'overlay (règle d'observation du cycle) : ces vérifications sont faites par l'utilisateur.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
