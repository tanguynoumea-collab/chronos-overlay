---
phase: 19
slug: nouvelle-doctrine-du-composite
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-09-12
---

# Phase 19 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~LastExact\|FullyQualifiedName~Composite\|FullyQualifiedName~Doctrine"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | **~2 min 15** · baseline mesurée : **652 tests / 0 échec** |

Note : la durée a triplé en phase 18 (56 s → ~2 min 15) parce que chaque test de `DiagnosticServiceTests`
exécute `BuildReportAsync`, qui balaie `%APPDATA%` / `%LOCALAPPDATA%` et fait un **poll UIA réel**. Dette
consignée, candidate phase 20 — ne pas la traiter ici.

## Sampling Rate

- Après chaque commit de tâche : commande rapide
- Après chaque vague : suite complète
- Avant vérification de phase : suite complète verte

## Tâche zéro recommandée par la recherche

Avant toute conception : **ajouter `CapturedAt` aux 3 providers qui ne le posent pas**
(`ChronosOAuthUsageProvider`, `ClaudeOAuthUsageProvider`, `ClaudeUsageObjectProvider` — seule la sonde
d'en-têtes le renseigne aujourd'hui), puis lancer la suite et **prendre la liste d'échecs comme inventaire
réel** des ~120 tests de providers non lus exhaustivement. 2 min 15 contre une journée d'hypothèses.

## Le piège le plus grave de la phase

**Horodater `ClaudeUsageObjectProvider` avec `now` au lieu du `capturedAt` du fichier ressusciterait le bug
des « 10 % »**, cette fois avec l'autorité d'une limite d'âge prétendument appliquée. Un test doit
explicitement charger un `usage.json` ancien et vérifier que son âge est celui du FICHIER, pas de la lecture.

## Contraintes de sécurité

- **Aucune requête réseau réelle depuis un test.**
- Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL de `%APPDATA%\Chronos\oauth.dat`.
  Contrôle avant/après chaque plan : **518 octets, mtime 1783863147**.
- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` — chemins depuis `Path.GetTempPath()`.
- Les fichiers SOURCE contenant `RefreshAsync` doivent rester exactement 2 (exclure `bin/` et `obj/`).

## Per-Task Verification Map

*Rempli par le planner le 2026-09-12. Statuts mis à jour par le plan 19-05 (porte de phase).*

Toutes les commandes se préfixent de `dotnet test Chronos.sln -v q --nologo`.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 19-01 T1 | 19-01 | 1 | EXA-02 | unit | `--filter "FullyQualifiedName~ClaudeUsageObjectProviderTests"` — *Un_usage_json_vieux_de_deux_mois_porte_l_age_du_FICHIER_et_non_celui_de_la_lecture*, *Un_usage_json_sans_capturedAt_laisse_l_age_INCONNU* | ✅ | ✅ passed |
| 19-01 T1 | 19-01 | 1 | EXA-02 | inventaire | *(suite complète)* — inventaire nominatif des ~120 tests de providers réellement impactés | ✅ | ✅ passed |
| 19-01 T2 | 19-01 | 1 | EXA-05 | unit | `--filter "FullyQualifiedName~LastExactStoreTests"` — *Aucun_fichier_signifie_qu_aucun_exact_n_a_JAMAIS_ete_obtenu*, *Un_releve_dont_la_fenetre_a_roule_prouve_tout_de_meme_qu_un_exact_a_ete_obtenu* | ✅ | ✅ passed |
| 19-01 T2 | 19-01 | 1 | EXA-02 | unit | `--filter "FullyQualifiedName~LastExactStoreTests"` — *Une_fenetre_exacte_SANS_instant_de_capture_n_est_pas_persistee* | ✅ | ✅ passed |
| 19-01 T3 | 19-01 | 1 | (coût) | unit | `--filter "FullyQualifiedName~SourceActiviteMemoiseeTests"` — durée de validité, passe unique, panne non levante | ✅ | ✅ passed |
| 19-02 T1 | 19-02 | 2 | DEL-04 | build | `dotnet build Chronos.sln -c Release` + suite complète au MÊME compte (ajouts purement additifs, zéro `required` de plus) | ✅ | ✅ passed |
| 19-02 T2 | 19-02 | 2 | EXA-02 | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` — branche 1 *Releve_sous_la_limite_est_exact_frais* ; branche 4b *Un_exact_vivant_sans_CapturedAt_est_incertifiable_et_ne_passe_pas_pour_frais* | ✅ | ✅ passed |
| 19-02 T2 | 19-02 | 2 | DEL-03 | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` — branche 2 *Trois_heures_sans_aucune_activite_reste_EXACT_et_non_perime* | ✅ | ✅ passed |
| 19-02 T2 | 19-02 | 2 | DEL-04 | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` — branche 3 *Activite_depuis_le_releve_donne_un_plancher_marque* ; branche 4a *Releve_anterieur_a_l_horizon_des_transcripts_est_indisponible_et_non_sous_evalue* | ✅ | ✅ passed |
| 19-02 T2 | 19-02 | 2 | EXA-04 | unit | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` — *Le_plancher_ne_gonfle_JAMAIS_l_utilization* : aucun delta n'est ajouté au chiffre | ✅ | ✅ passed |
| 19-02 T3 | 19-02 | 2 | EXA-04 | guard | `--filter "FullyQualifiedName~GardesDoctrineTests"` — *Aucun_rapport_entre_un_comptage_de_tokens_et_un_plafond_dans_Services_et_Models*, falsifiable par mutation réelle puis révocation | ✅ | ✅ passed |
| 19-02 T3 | 19-02 | 2 | (non-régression) | guard | `--filter "FullyQualifiedName~GardesDoctrineTests"` — *Toute_propriete_de_UsageSnapshot_est_nommee_dans_la_recomposition_du_composite* ; *Le_repli_le_plus_interne_est_bien_celui_a_anciennete_non_bornee* | ✅ | ✅ passed |
| 19-03 T1 | 19-03 | 3 | DEL-03, DEL-04 | unit | `--filter "FullyQualifiedName~LastExactUsageProviderTests"` — *Un_releve_de_deux_heures_SANS_activite_depuis_reste_EXACT* ; *Un_releve_de_deux_heures_AVEC_activite_depuis_devient_un_plancher_marque* ; *Un_plancher_n_est_JAMAIS_persiste_comme_exact* | ✅ (tests réécrits) | ✅ passed |
| 19-03 T1 | 19-03 | 3 | EXA-02 | integration | `--filter "La_sonde_d_en_tetes_est_le_PRIMAIRE"` et `--filter "Host_resout_et_dispose_les_singletons"` — la doctrine est câblée sans casser la garde de position de la phase 18 | ✅ | ✅ passed |
| 19-03 T2 | 19-03 | 3 | (coût) | unit | `--filter "FullyQualifiedName~LastExactUsageProviderTests"` — *Chemin_nominal_ne_lit_JAMAIS_les_transcripts* (0 passe) ; *Les_deux_fenetres_degradees_partagent_UNE_SEULE_passe_disque* (1 passe) ; *Une_source_d_activite_en_panne_degrade_sans_lever* | ✅ | ✅ passed |
| 19-03 T2 | 19-03 | 3 | EXA-05 | unit | `--filter "FullyQualifiedName~LastExactUsageProviderTests"` — *Sans_magasin_le_snapshot_declare_qu_aucun_exact_n_a_JAMAIS_ete_obtenu* ; *Un_magasin_portant_un_releve_declare_qu_un_exact_a_deja_ete_obtenu* ; null si le magasin est illisible | ✅ | ✅ passed |
| 19-03 T3 | 19-03 | 3 | DEL-04 | unit | `--filter "FullyQualifiedName~WeeklyRecalibrationTests"` — *Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre* (9 tests attendus) | ✅ | ✅ passed |
| 19-04 T1 | 19-04 | 4 | DEL-04, EXA-04 | unit | `--filter "FullyQualifiedName~WindowGaugeViewModelTests"` — *UtilizationText_plancher_prefixe_superieur_ou_egal* : « ≥ 80 % » pour un plancher, « 80 % » pour un exact | ✅ | ✅ passed |
| 19-04 T1 | 19-04 | 4 | DEL-04 | unit | `--filter "FullyQualifiedName~CadranBindingTests"` — les 3 tests de tokens réécrits sur `TokensDepuisReleve` | ✅ | ✅ passed |
| 19-04 T2 | 19-04 | 4 | EXA-05 | unit | `--filter "FullyQualifiedName~MainViewModelTests"` — matrice de 6 cas de `AfficherInvitationConnexion` : false / true / null, chiffre disponible, état Deconnecte, ordre des deux canaux | ✅ | ✅ passed |
| 19-04 T2 | 19-04 | 4 | EXA-05 | binding | `--filter "FullyQualifiedName~CadranBindingTests"` — *L_invitation_est_bindee_sur_ReconnecterCommand_et_JAMAIS_sur_LoginClaudeCommand* ; *L_invitation_reste_COLLAPSED_quand_un_exact_a_deja_ete_obtenu* | ✅ | ✅ passed |
| 19-02 T2 (ajout) | 19-02 | 2 | EXA-02 | guard | `--filter "FullyQualifiedName~DoctrineFraicheurTests"` — *La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde* : 13e test ajouté hors plan par 19-02, verrouille le `key_link` « limite dérivée, jamais recopiée » | ✅ | ✅ passed |
| 19-04 T1 (ajout) | 19-04 | 4 | EXA-04 | guard | `--filter "FullyQualifiedName~CadranBindingTests"` — *EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran* : garde de non-retour comportementale de l'estimation absolue | ✅ | ✅ passed |
| 19-04 T1 (ajout) | 19-04 | 4 | DEL-04 | unit | `--filter "FullyQualifiedName~CadranBindingTests"` — *Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre* : test **renommé** par 19-04 (ex-`…_le_tilde_du_pourcentage_…`), la marque est par fenêtre | ✅ | ✅ passed |
| 19-05 T1 | 19-05 | 5 | toutes | gate | suite complète + les 5 gardes permanentes + contrôle du coffre + un seul rafraîchisseur | ✅ | ✅ passed |
| 19-05 T2 | 19-05 | 5 | EXA-02, EXA-05 | **manual** | non automatisable — bascule « 10 % » vers « indisponible + invitation » constatée sur la machine réelle | — | ⏳ **à vérifier par l'utilisateur** (protocole dans `19-05-SUMMARY.md`) |

## Les quatre branches de la doctrine — chacune doit avoir un test qui tombe si la branche disparaît

| Branche | Ce qui doit être vrai | Exigence |
|---------|----------------------|----------|
| 1. Exact frais | Relevé sous la limite d'âge : affiché tel quel | EXA-02 |
| 2. Encore rigoureusement valide | Aucune réponse assistant depuis l'horodatage du relevé → **encore exact**, pas périmé | DEL-03 |
| 3. Plancher marqué | Activité depuis → « **≥ N %** », incertitude **unilatérale**, `Utilization` **jamais gonflée** | DEL-04 |
| 4. Indisponible | Sinon. Et si aucun exact n'a **jamais** été obtenu : invitation à se connecter, **jamais un pourcentage** | EXA-05 |

**DEL-04 n'a pas de réponse numérique honnête et n'en a pas besoin** : le delta change la NATURE du chiffre,
il ne s'y ajoute pas. Mesure à l'appui : 643 649 933 tokens sur 5 h contre l'ancien plafond de 230 000 000
= 280 %. Conséquence imposée à la phase 20 : **pas d'arc de delta, pas de barre d'erreur.**

**EXA-04 est une interdiction, pas une fonctionnalité** : aucune utilization absolue dérivée d'un comptage de
tokens ne doit réapparaître. `tokens / plafond` a été supprimé en phase 16 — une garde de balayage du texte
source doit le maintenir mort.

## Gardes permanentes

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté Services | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert |
| Composition DI | `--filter "FullyQualifiedName~CompositionRootTests"` | vert |
| Position de la sonde | `--filter "La_sonde_d_en_tetes_est_le_PRIMAIRE"` | vert — garde falsifiable de la phase 18, **ne pas la casser silencieusement** |
| Normalisation unique | `--filter "FullyQualifiedName~NormalisationUniqueTests"` | vert — aucune conversion divergente réintroduite |
| Coffre intact | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec |

Toutes les commandes `--filter` se préfixent de `dotnet test Chronos.sln -v q --nologo`.

## Wave 0 Requirements

*Tous couverts par le plan 19-01 (wave 1), en tête de phase — voir la carte ci-dessus.*

- [x] `CapturedAt` posé par les 3 providers qui l'omettent, puis inventaire des échecs réels
      -> **19-01 T1**
- [x] Horloge injectée (`FakeClock` existe) sur tout chemin décisionnel : les 4 branches doivent être
      testables de façon **déterministe** -> **19-02 T2** : `DoctrineFraicheur` est PURE, `now` est
      toujours un paramètre et jamais une horloge interne (un critère grep l'impose)
- [x] Fixture `usage.json` ancienne, pour prouver que l'âge retenu est celui du FICHIER
      -> **19-01 T1**, `tests/Chronos.Tests/TestData/usage-ancien.json`
      (`capturedAt` = 1783673700000 = 2026-07-10T08:55:00Z ; `resets_at` = 1783684500)
- [x] Fixtures de transcripts : activité depuis T / aucune activité depuis T / T antérieur à l'horizon
      (`Covers(T) == false`, où le delta ne peut pas être garanti)
      -> **19-01 T3** (`Fakes/FakeTranscriptActivitySource.cs` : journal programmable, compteur de
      passes, mode panne) et **19-02 T2** (helper `Journal(...)` des `DoctrineFraicheurTests`)

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Le chiffre affiché est le bon après deux mois de panne | EXA-02 | Dépend de l'état réel de la machine | Après reconnexion, comparer le chiffre du cadran avec `/usage` dans Claude Code |

## Validation Sign-Off

- [x] Les 4 branches ont chacune un test qui tombe si la branche est supprimée
- [x] Aucun pourcentage n'est jamais inventé, dans aucune branche
- [x] `Utilization` n'est jamais gonflée par un delta
- [x] La garde de position de la sonde (phase 18) est toujours verte
- [x] `tokens / plafond` reste mort (garde de balayage du texte source)

## Porte de phase — relevé du 2026-09-12 (plan 19-05)

Toutes les commandes ci-dessous ont été **réellement exécutées** ce jour, dans cet ordre.

### Suite complète — deux exécutions consécutives

| Exécution | Résultat | Durée |
|-----------|----------|-------|
| 1 | **699 réussis / 699, 0 échec, 0 ignoré** | 2 min 9 s |
| 2 | **699 réussis / 699, 0 échec, 0 ignoré** | 2 min 8 s |

Deux passes exigées parce que la phase a ajouté des `[WpfFact]` : le chargeur BAML de WPF a un précédent
documenté de course sous parallélisme xUnit (décision 16-03). Aucune variation entre les deux passes.

### Bilan de comptage depuis la baseline d'entrée de phase

| Étape | Total | Écart | Justification nominative |
|-------|-------|-------|--------------------------|
| Baseline d'entrée de phase 19 | 652 | — | mesurée avant 19-01 |
| Après 19-01 | 664 | **+12** | +2 `ClaudeUsageObjectProviderTests`, +4 `LastExactStoreTests`, +6 `SourceActiviteMemoiseeTests` |
| Après 19-02 | 680 | **+16** | +13 `DoctrineFraicheurTests`, +3 `GardesDoctrineTests` |
| Après 19-03 | 688 | **+8** | +1 scission du test 3, +5 `LastExactUsageProviderTests`, +2 `WeeklyRecalibrationTests` |
| Après 19-04 | 699 | **+11** | +2 `WindowGaugeViewModelTests`, +1 garde `CadranBindingTests`, +6 `MainViewModelTests`, +2 `[WpfFact]` d'invitation |
| **Mesuré par la porte** | **699** | — | **concorde exactement : 652 + 12 + 16 + 8 + 11 = 699** |

**Zéro test supprimé, zéro test ignoré sur toute la phase.** 8 tests réécrits sur place (4 en 19-03,
4 en 19-04) : la preuve est conservée, seule la matière change. Aucun écart inexpliqué.

### Les cinq gardes permanentes

| Garde | Commande | Résultat |
|-------|----------|----------|
| Pureté `Services`/`Models` | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | **2 / 2 vert** |
| Composition DI | `--filter "FullyQualifiedName~CompositionRootTests"` | **5 / 5 vert** |
| Position de la sonde (phase 18) | `--filter "La_sonde_d_en_tetes_est_le_PRIMAIRE"` | **1 / 1 vert** |
| Normalisation unique | `--filter "FullyQualifiedName~NormalisationUniqueTests"` | **3 / 3 vert** |
| Gardes de doctrine | `--filter "FullyQualifiedName~GardesDoctrineTests"` | **3 / 3 vert** |

### Les quatre branches de la doctrine

`--filter "FullyQualifiedName~DoctrineFraicheurTests"` → **13 / 13 vert**.

### Détail par groupe de tests de la carte

| Groupe | Résultat |
|--------|----------|
| `ClaudeUsageObjectProviderTests` | 6 / 6 |
| `LastExactStoreTests` | 17 / 17 |
| `SourceActiviteMemoiseeTests` | 6 / 6 |
| `LastExactUsageProviderTests` | 15 / 15 |
| `WeeklyRecalibrationTests` | 9 / 9 |
| `WindowGaugeViewModelTests` | 19 / 19 |
| `CadranBindingTests` | 14 / 14 |
| `MainViewModelTests` | 42 / 42 |
| `CompositeUsageProviderTests` | 14 / 14 (intact, aucune retouche de toute la phase) |
| `Host_resout_et_dispose_les_singletons` | 1 / 1 |

### Sécurité et intégrité des données de production

| Contrôle | Attendu | Mesuré |
|----------|---------|--------|
| `stat` de `oauth.dat` AVANT la porte | `518 1783863147` | `518 1783863147` OK |
| `stat` de `oauth.dat` APRÈS la porte | `518 1783863147` | `518 1783863147` OK |
| Un seul rafraîchisseur : nombre de fichiers source contenant `RefreshAsync` | `2` | `2` OK — `ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs` |
| `usage.json` de production inchangé | 77 o, mtime 1783666519 | 77 o, mtime 1783666519 OK |
| Requête réseau réelle | aucune | aucune OK |
| `git status --short` en entrée de plan | vide | vide OK — aucun fichier `src/` ni `tests/` touché par ce plan |

### État réel de la machine, relevé en lecture seule

```
$APPDATA/Chronos/usage.json       {"five_hour":{"used_percentage":10,"resets_at":9},"capturedAt":1783666519131}
                                  77 octets, mtime 1783666519 (2026-07-10)
$APPDATA/Chronos/last-exact.json  ABSENT
$APPDATA/Chronos/oauth.dat        518 octets, mtime 1783863147
```

`capturedAt = 1783666519131` ms = **2026-07-10**, soit **64 jours** d'âge au 2026-09-12. La limite d'âge de
la doctrine est de 6 minutes, et l'horizon des transcripts de 8 jours : `Covers(T)` est donc **faux**, ce qui
place ce relevé en **branche 4 (indisponible)** et non en plancher. `last-exact.json` étant absent,
`UnExactADejaEteObtenu` vaut **false**, ce qui allume l'invitation à se connecter. C'est la prédiction que la
vérification humaine doit confirmer de visu.

### Ce que la porte automatisée ne prouve PAS

La ligne `19-05 T2` de la carte reste `⏳ à vérifier par l'utilisateur`. Le constat de la bascule sur la
machine réelle n'a **pas** été réalisé : il exige de lancer l'overlay, ce qui déclencherait la purge des
25 groupes de hooks de `~/.claude/settings.json` (codée en phase 15) hors supervision de l'utilisateur.
Le protocole exact est consigné dans `19-05-SUMMARY.md`, section « À VÉRIFIER PAR L'UTILISATEUR ».
