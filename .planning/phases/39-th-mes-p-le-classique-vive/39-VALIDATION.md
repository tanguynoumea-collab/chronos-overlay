---
phase: 39
slug: th-mes-p-le-classique-vive
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-10-03
---

# Phase 39 — Stratégie de validation

> Contrat de validation de la phase : échantillonnage du retour pendant l'exécution.

---

## Infrastructure de test

| Propriété | Valeur |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, collection `"XAML WPF"` sérialisée) |
| **Fichier de config** | `tests/Chronos.Tests/Chronos.Tests.csproj` (attribut `CheminSourcesChronos` déjà injecté pour les gardes de source) |
| **Commande rapide** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~ThemingTests\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadransThemeBinding"` |
| **Suite complète** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Durée estimée** | rapide ~40 s ; complète ~4-6 min (matrice sessions × thèmes passée de 72 à 120 montages) |

---

## Fréquence d'échantillonnage

- **Après chaque commit de tâche :** commande rapide (ou le filtre `<automated>` de la tâche).
- **Après chaque vague :** suite complète. Vagues strictement séquentielles (01 → 02 → 03 → 04) : une tâche RED rend le projet de test non compilable et bin/obj sont partagés (pas de worktree).
- **Avant `/gsd:verify-work` :** suite complète verte, build Debug et Release à 0 warning.
- **Latence max de retour :** 60 s pour les filtres de tâche.

---

## Carte de vérification par tâche

| Task ID | Plan | Vague | Exigence | Type | Commande automatisée | Fichier existe | Statut |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 39-01-01 | 01 | 1 | THM-01/02/03/04 | unit (RED) | `dotnet build tests/Chronos.Tests/Chronos.Tests.csproj` (échec attendu sur CategorieTheme/ContrasteWcag) | ✅ ThemingTests à étendre | ⬜ pending |
| 39-01-02 | 01 | 1 | THM-01/02/03 | unit | `dotnet test … --filter "FullyQualifiedName~ThemingTests.Catalogue\|…Chaque_theme\|…Contraste\|…Le_gris\|…Le_rouge\|…Ancrage_minuit"` | ❌ W0 (tâche 01-01) | ⬜ pending |
| 39-01-03 | 01 | 1 | THM-04 | unit + WPF | `dotnet test … --filter "FullyQualifiedName~ThemingTests\|FullyQualifiedName~SessionStylesBindingTests\|FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~GardeTokens"` | ❌ W0 (tâche 01-01) | ⬜ pending |
| 39-02-01 | 02 | 2 | THM-01 | WPF | `dotnet test … --filter "FullyQualifiedName~ThemingTests"` | ✅ à étendre | ⬜ pending |
| 39-02-02 | 02 | 2 | THM-01 | WPF (STA) | `dotnet test … --filter "FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~GardeTokensReglagesTests\|FullyQualifiedName~ReglagesBindingTests\|FullyQualifiedName~ThemingTests"` | ✅ à adapter + nouveaux tests | ⬜ pending |
| 39-03-01 | 03 | 3 | THM-04 | garde source + WPF (RED) | `dotnet build tests/Chronos.Tests/Chronos.Tests.csproj` (échec attendu sur WaitBrush) | ❌ W0 (crée GardeCouleursCadransTests, CadransThemeBindingTests) | ⬜ pending |
| 39-03-02 | 03 | 3 | THM-04 | build | `dotnet build src/Chronos/Chronos.csproj -c Debug` (0 warning) | — | ⬜ pending |
| 39-03-03 | 03 | 3 | THM-04 | garde source + WPF | `dotnet test … --filter "FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadransThemeBinding\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~GardeGestesCadranTests"` | ❌ W0 (tâche 03-01) | ⬜ pending |
| 39-04-01 | 04 | 4 | THM-03 | unit (STA) | `dotnet test … --filter "FullyQualifiedName~UtilizationToBrushConverterTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~ServicesLayerPurityTests"` | ✅ à adapter | ⬜ pending |
| 39-04-02 | 04 | 4 | THM-03/04 | WPF | `dotnet test … --filter "FullyQualifiedName~VueJourBindingTests\|…VueSemaineBindingTests\|…VueQuatreSemainesBindingTests\|…HistoriqueBindingTests\|…GardeTokensHistoriqueTests\|…HonneteteHistoriqueTests\|…PistesHistoriqueTests"` | ✅ à adapter | ⬜ pending |

*Statut : ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Exigences de la vague 0

Les tâches RED créent les tests avant le code (pas de vague 0 séparée : chaque plan ouvre par sa tâche RED).

- [ ] `tests/Chronos.Tests/ThemingTests.cs` — nouveaux `[Fact]` : 15 thèmes / 3 catégories, palettes exactes (THM-02), contraste, minimalité, teinte, helper WCAG (THM-03), tokens et replis (THM-04) — tâche 39-01-01
- [ ] `tests/Chronos.Tests/GardeCouleursCadransTests.cs` — garde « aucune couleur littérale », anti-mutisme 4 xaml + 4 cs, auto-test des regex (THM-04) — tâche 39-03-01
- [ ] `tests/Chronos.Tests/CadransThemeBindingTests.cs` — 4 vues × 15 thèmes + rafraîchissement dynamique (THM-04) — tâche 39-03-01
- [ ] `tests/Chronos.Tests/ReglagesWindowTests.cs` — titres de groupes, collecte par arbre, commande par groupe (THM-01) — tâche 39-02-02
- Aucune installation de framework nécessaire.

---

## Vérifications manuelles uniquement

| Comportement | Exigence | Pourquoi manuel | Instructions |
|----------|-------------|------------|-------------------|
| Revue visuelle 15 thèmes × 5 styles : gris épuisé visible, Braises/Fusible/Marée/Volets thémés, chiffres de plaque Volets lisibles | THM-03, THM-04 | Jugement esthétique (critère §9.5 du plan) ; l'agent ne lance jamais l'overlay | L'utilisateur lance `Chronos.exe --cadrans`, parcourt la ComboBox de thèmes, pousse un quota à 100 % ; vérifier aussi l'écart connu « épuisé ≈ neutre » (gris neutre volontairement inchangé) |
| Section Thème : trois groupes PÂLE / CLASSIQUE / VIVE, étiquette « THÈME » jugée utile ou redondante | THM-01 | Lisibilité de la hiérarchie de titres | Ouvrir Réglages → Apparence, à 640 px puis plein écran |

---

## Validation

- [x] Toutes les tâches ont un `<automated>` ou une dépendance RED
- [x] Continuité : jamais 3 tâches consécutives sans vérification automatisée
- [x] Les références MISSING sont couvertes par les tâches RED
- [x] Aucun mode watch
- [x] Latence de retour < 60 s par tâche
- [x] `nyquist_compliant: true` dans le frontmatter

**Approbation :** en attente
