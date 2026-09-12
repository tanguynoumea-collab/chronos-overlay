# Phase 20 : Honnêteté visible — cadran & diagnostic — Recherche

**Researched:** 2026-09-12
**Domain:** sémiologie graphique sur WPF contraint (170 px, 5 styles × 9 thèmes × 2 modes) + archéologie de code
**Confidence:** HIGH (tout ce qui est affirmé ici est lu dans le dépôt ou mesuré sur cette machine ; les deux
seules zones MEDIUM sont nommées en fin de document)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Claude's Discretion** — tous les choix d'implémentation sont à la discrétion de Claude
(`workflow.skip_discuss=true`, `workflow.ui_phase=false` : pas de contrat UI séparé).

**Loi d'encodage du cadran — contrainte de design ÉTABLIE, à respecter**
> Le projet a une loi d'encodage décidée lors de la refonte visuelle (idéation llm-council) :
> **temps = géométrie, quota = luminance.** Toute nouvelle information visuelle doit s'y conformer plutôt que
> d'inventer un troisième canal. Les tokens de design sont dans `src/Chronos/Resources/DesignTokens.xaml` et
> `src/Chronos/Theming/ChronosTheme.cs`.

**Contrainte imposée par la phase 19, non négociable**
> **DEL-04 : pas d'arc de delta, pas de barre d'erreur.** Le plancher n'est pas un nombre augmenté d'une marge :
> « 42 % » devient « ≥ 42 % », incertitude **unilatérale**. Mesure à l'appui : 643 649 933 tokens sur 5 h contre
> l'ancien plafond de 230 000 000 = 280 % — un delta chiffré serait une fiction. La sémiologie doit donc
> exprimer « au moins », pas « environ ± x ».

**Surface réelle à couvrir**
> L'overlay est **compact (170 px)**, sans barre de titre, sur fenêtre transparente. Il existe :
> - **2 modes de cadran** (Normal épuré par défaut / Étendu 3 anneaux),
> - **5 styles de cadran** (Arcs, Braises, Fusible, Marée, Volets),
> - **3 thèmes de couleur**.
> Toute distinction visuelle doit rester lisible dans **toutes** ces combinaisons, sans casser la compacité ni
> les tokens de design validés. Deux pastilles occupent déjà le coin bas-droite du `Grid` racine (déconnexion,
> invitation à se connecter) et sont mutuellement exclusives.

> ⚠️ **Correction factuelle (vérifiée) :** `ThemeCatalog.All` compte **9 thèmes**, pas 3
> (`ThemingTests.Catalogue_a_six_themes_aux_cles_uniques` asserte `Assert.Equal(9, …)`). La matrice réelle est
> donc **5 styles × 9 thèmes × 2 modes = 90 combinaisons**. Voir « Piège n° 1 ».

**Contraintes projet**
> - MVVM strict : aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`). Le signal visuel
>   passe par les ViewModels.
> - `Transparent` EST hit-testable en WPF ; seul `{x:Null}` ne l'est pas (pas besoin de `#01000000`).
> - Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
> - **Ne JAMAIS présenter une estimation comme un chiffre exact** — Core Value du projet, et raison d'être de
>   tout ce milestone.
> - UI et commentaires en **français**.
> - **Baseline à l'entrée de phase : 699 tests xUnit verts.**

**SÉCURITÉ**
> - Aucune requête réseau réelle depuis un test.
> - Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL de `%APPDATA%\Chronos\oauth.dat` —
>   contrôle : **518 octets, mtime 1783863147**.
> - Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
> - **Ne PAS lancer l'overlay** sans supervision : cela déclencherait la purge des 25 hooks de
>   `~/.claude/settings.json` codée en phase 15.
> - Les fichiers SOURCE contenant `RefreshAsync` doivent rester exactement 2.
> - **Ne pas binder quoi que ce soit sur `LoginClaudeCommand`** : elle BASCULE, et un clic supprimerait le
>   coffre de jetons. `ReconnecterCommand` est la commande dédiée.

### Claude's Discretion

Tous les choix d'implémentation. La discussion est désactivée. Aucun contrat UI séparé.

### Deferred Ideas (OUT OF SCOPE)

> - **Vérifications manuelles héritées, à présenter à l'utilisateur en fin de milestone** : la bascule visuelle
>   réelle (phase 19), le 429 réel portant les en-têtes (phase 18, HDR-02), le parcours de reconnexion de bout
>   en bout et le contraste des pastilles sur fond d'écran réel (phase 17).
> - **Piste d'économie** : si `/api/oauth/usage` porte lui aussi la famille d'en-têtes `unified`, le coût de la
>   sonde (≈ 288 micro-requêtes/jour) peut être supprimé. Le diagnostic liste désormais les noms d'en-têtes
>   reçus — la réponse viendra au premier rafraîchissement réussi.
> - **Préavis avant saturation (~90 %) et notification au reset** — Future Requirements, hors v1.5.

**Ajout de cette recherche à la liste des différés** (argumenté en Question 3) : la **lecture en queue de
fichier des transcripts** et la descente de `LimiteAge` vers 2 min. C'est une optimisation d'E/S, pas une
exigence d'honnêteté visible ; elle n'est pas nécessaire pour résoudre la contradiction n° 5.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **EXA-03** | Le cadran **distingue visuellement** trois états : chiffre exact frais, chiffre exact daté, état indisponible. `IsStale` (calculé mais bindé nulle part) devient un signal réel à l'écran. | Question 1 (trois marques / quatre apparences), Question 2 (inventaire des canaux réellement disponibles par style, **mesuré dans le code**), Question 3 (mise à mort de `IsStale`), Question 7 (`DataUnavailable`), Pattern 1 & 2, Piège 1 à 5 |
| **EXA-06** | Le **diagnostic** indique quelle source alimente réellement l'affichage, et depuis quand. | Question 4 (`WindowState.Source`, garde vérifiée non impactée, 0 site de construction à retoucher), Pattern 3, Code Example 3 |

**Critères de succès de la ROADMAP** (rappel, ils gouvernent la vérification) :
1. Trois états lisibles d'un coup d'œil, **sans lire de texte**.
2. Cohérent dans les deux modes et les thèmes, sans casser la compacité ni les tokens validés.
3. Diagnostic sans ambiguïté : **en-têtes / endpoint OAuth / pont statusLine / dernier exact persisté /
   dernier exact + delta**, et depuis quand.
4. Aucune fuite de WPF dans les services ; `ServicesLayerPurityTests` reste vert.
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

| Directive | Conséquence pour cette phase |
|---|---|
| MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services` | Tout nouveau signal visuel naît d'une `[ObservableProperty]` sur `WindowGaugeViewModel` ou `MainViewModel` |
| Rendu des arcs en **XAML pur** (`Path`/`ArcSegment`), **aucune dépendance native** | La marque du plancher aux Anneaux se fait par `StrokeDashArray` sur `RingArc` (qui dérive de `Shape`), jamais par un effet ni une lib |
| `utilization`/`resets_at` prioritaires ; **ne jamais présenter une estimation comme exacte** | Le plancher doit porter une marque VISUELLE et pas seulement typographique |
| **UI et commentaires en français** | Nommer `EstPlancher` / `EstDate` / `SourceUsage`, pas `IsFloor` / `IsStale2` |
| « Activer frontend-design + windows-wpf sur les tâches ui » | Les tâches de dessin doivent charger ces skills ; **aucun répertoire `.claude/skills/` n'existe dans ce dépôt** (vérifié) — ce sont des skills globaux |
| `PublishTrimmed=false`, `AllowsTransparency` ⇒ rendu **logiciel** | Pas de `BlurEffect`, `DropShadowEffect` ni `Storyboard` continue sur `MainWindow` ; un `StrokeDashArray` ou un `DrawingBrush` tuilé sont gratuits |
| `Assembly.Location` interdit | Toute nouvelle garde balayant le texte source doit passer par l'attribut MSBuild `CheminSourcesChronos` (motif `GardesDoctrineTests` / `NormalisationUniqueTests`) |

---

## Summary

Cette phase n'a **aucun problème technique** : elle a un problème de **décisions**. Le code nécessaire est
petit (quelques dizaines de lignes), les dépendances sont nulles, et la plomberie de la phase 19 est déjà
posée. Ce qui manque, ce sont sept arbitrages que le planner doit rendre explicitement — et la recherche les
tranche ci-dessous, chacun avec l'élément du dépôt ou la mesure qui le justifie.

Trois découvertes changent la forme du plan :

1. **Le canal « texture » n'est pas à inventer : il existe déjà dans 4 styles sur 5.** `EmberRingControl`,
   `FuseBar`, `TideColumn` et `CadranVoletsView` portent tous une DP `Estimated` bindée sur
   `WindowGaugeViewModel.IsEstimated`, qui rend un grain / pointillé. Or depuis la phase 19,
   `SourceReliability.Estimated` n'est produit qu'en **un seul endroit de la production**
   (`DoctrineFraicheur` branche 3 — vérifié : 1 seul `SourceReliability.Estimated` affectant en `src/`).
   Donc **`IsEstimated` signifie déjà exactement « plancher »**, et 4 styles sur 5 marquent déjà le plancher
   correctement sans le savoir. Il manque **les Anneaux** — un `StrokeDashArray`.

2. **Le nom de la source se pose sur `WindowState`, et il ne casse rien.** La garde
   `Toute_propriete_de_UsageSnapshot_est_nommee_dans_la_recomposition_du_composite` porte, mot pour mot, sur
   les propriétés d'**`UsageSnapshot`** — pas de `WindowState`. Et `Best()` rend l'instance gagnante **par
   référence** : un champ de `WindowState` traverse gratuitement les trois composites imbriqués (précédent
   documenté : `StatutServeur`, `Provenance`). Tous les `new WindowState` (6 en `src/`, 42 en `tests/`) sont
   des initialiseurs d'objet n'exigeant que `Kind` et `Reliability` : **une propriété `init` nullable en plus
   casse 0 site sur 48.**

3. **La dette de durée des tests est 8× pire que documentée, et elle se lève pour 0 site de construction.**
   Mesuré sur cette machine, `--no-build` : les **692 tests hors `DiagnosticServiceTests` durent 2 secondes** ;
   les **7 tests de `DiagnosticServiceTests` durent 2 min 8 s**, soit **98,4 % du temps de la suite**. Et le
   coupable n'est pas celui qu'on croit : décomposition d'un `BuildReportAsync` → **`FindTokenVaults` 17 703 ms**
   (94 %), poll UIA 936 ms (5 %), tout le reste **< 50 ms cumulés**. Enfin, les phases 17 et 18 ont déjà établi
   dans ce fichier le protocole d'extension à **coût zéro** (paramètre optionnel en dernière position →
   « les 10 sites de construction compilent sans retouche »). L'appliquer une troisième fois coûte
   **0 site modifié**, pas 10.

**Primary recommendation :** livrer la phase en trois vagues — (1) le modèle (`SourceUsage` sur `WindowState`,
`Provenance` remontée au VM, mise à mort d'`IsStale`) ; (2) le dessin (marque du plancher aux Anneaux, marque
d'âge, mot « indisponible », rangée de pastilles) ; (3) le diagnostic + la levée de la dette de durée. Aucune
dépendance nouvelle. Aucun fichier de `Services/` ne gagne un type WPF.

---

## Standard Stack

### Core — rien à ajouter

| Library | Version installée | Rôle dans cette phase | Pourquoi on n'ajoute rien |
|---|---|---|---|
| .NET / WPF | `net8.0-windows`, `UseWPF` | `Shape.StrokeDashArray`, `DrawingBrush` tuilé, `FrameworkElement.OnRender` | Tout ce dont la phase a besoin est dans le framework |
| CommunityToolkit.Mvvm | 8.4.2 (déjà référencé) | `[ObservableProperty]` pour les nouveaux signaux | Générateurs de source, zéro réflexion |
| xunit | 2.9.2 | `[Fact]` | — |
| Xunit.StaFact | 1.1.11 | `[WpfFact]` (thread STA, obligatoire pour construire `MainWindow`) | — |

**Installation : aucune.** `dotnet add package` n'a rien à faire dans cette phase. Toute PR de la phase 20 qui
touche un `.csproj` est suspecte.

### Alternatives écartées

| Au lieu de | On pourrait | Pourquoi NON ici |
|---|---|---|
| `StrokeDashArray` sur `RingArc` | `OpacityMask` / `BlurEffect` pour « voiler » un arc daté | `AllowsTransparency=True` force le rendu **logiciel** : tout effet est payé en CPU à chaque tick de 1 s. `StrokeDashArray` est gratuit (géométrie) |
| Marque dans la couche racine du `Grid` | Dupliquer la marque dans les 5 `UserControl` | 1 implémentation contre 5 × 2 modes ; et les 4 `UserControl` alternatifs **codent leurs couleurs en dur** (voir Piège 1) |
| Nouveau membre d'enum sur `ProvenanceReleve` | Réutiliser `ProvenanceReleve` pour porter le nom de la source | `Provenance` répond « qu'a-t-on vérifié », la source répond « qui l'a produit ». Les fusionner produirait un enum produit cartésien de 15 membres (5 sources × 3 états) |
| `Chronos.Text` pour le vocabulaire FR des sources | Mapper dans `DiagnosticService` **et** dans le ViewModel | Deux mappings divergent — le dépôt a déjà tranché ce point (`WindowGaugeViewModel.LibelleStatut`, réutilisé par `MainViewModel` au lieu d'être refait) |

---

## Les huit questions ouvertes — tranchées

### Question 1 — Combien d'apparences distinctes ? Le plancher est-il « daté » ?

**Réponse : QUATRE apparences, TROIS marques — parce que deux marques se COMPOSENT.**

La contradiction « la doctrine produit 4 situations, EXA-03 n'en nomme que 3 » vient d'une confusion d'axes.
Les deux nomenclatures ne découpent pas la même chose :

| Situation (doctrine, phase 19) | Axe « comment a-t-on certifié ? » | Axe « peut-on croire le chiffre ? » (celui d'EXA-03) |
|---|---|---|
| `Frais` | âge < 6 min, aucune E/S | **exact** |
| `EncoreValide` | âge > 6 min **mais** zéro réponse assistant depuis T | **exact** (prouvé, pas supposé) |
| `PlancherAvecActivite` | âge > 6 min **et** activité depuis T | **borne inférieure** |
| `null` + `Unavailable` | rien de certifiable | **aucun chiffre** |

Sur l'axe d'EXA-03, `Frais` et `EncoreValide` sont **le même état de confiance**. Les distinguer par une
dégradation visuelle serait une **fausse démotion** — exactement l'inverse de ce que DEL-03 a conquis
(« trois heures sans activité reste EXACT et non périmé », test à l'appui).

**Le plancher n'est pas « daté ». Il est incomplet vers le haut.** L'argument tient, et il est même plus fort
que ne le dit le CONTEXT, parce qu'il y a une **relation d'inclusion vérifiable dans le code** :
`DoctrineFraicheur.Statuer` ne peut atteindre la branche 3 qu'après avoir franchi
`if (age <= LimiteAge) return … Frais`. Donc :

> **plancher ⊂ daté.** Tout plancher est vieux ; tout vieux n'est pas un plancher.

D'où la sémiologie, qui n'invente aucune quatrième catégorie :

| # | Apparence | Marques portées | Ce que ça dit |
|---|---|---|---|
| 1 | **Exact frais** | **aucune** | Le cas nominal. *L'absence de marque EST la marque de la fraîcheur* — un overlay qui décore son cas normal fabrique du bruit permanent |
| 2 | **Exact daté** (`EncoreValide`) | **M-âge** | « Ce chiffre a été relevé il y a un moment, et j'ai vérifié qu'il est toujours juste » |
| 3 | **Plancher** (`PlancherAvecActivite`) | **M-âge + M-plancher** | « Vieux, ET il s'est passé des choses depuis : c'est un minimum » |
| 4 | **Indisponible** | **M-indispo** (pas de chiffre + le mot) | « Je ne sais pas » |

Trois marques, quatre apparences. La superposition en n° 3 n'est **pas** une redondance : les deux marques
disent deux faits différents, et leur composition est littéralement vraie.

**Conséquence de dessin, à graver dans le plan :** M-âge et M-plancher doivent être portées par des **canaux
orthogonaux** (l'une ne doit pas être une intensification de l'autre), sinon leur superposition serait
illisible.

### Question 2 — Quel canal visuel reste ? Inventaire réel par style

Inventaire **lu dans le code**, pas supposé :

| Style | Canal temps (géométrie) | Canal quota (luminance) | Canal TEXTURE **déjà câblé** | Élément « piste/éteint » libre |
|---|---|---|---|---|
| **Anneaux** | `RingArc.Fraction` ← `FractionElapsed` / `DayFraction` | `Stroke` ← `ValueBrush` | **AUCUN** ❌ | `Piste5h` / `PisteHebdo` / `Piste24h` |
| **Braises** | nb de braises allumées ← `FractionRemaining` | `QuotaBrush` du pip | ✅ `Estimated` → pip en **contour pointillé** (`DashStyle{1.4,1.4}`) | `AshBrush` (cendre) |
| **Fusible** | position du front ← `Fraction` | `QuotaBrush` + `CordThickness` | ✅ `Estimated` → cordon à **alpha 0,5** + trait brisé | `TrackBrush` (sillon) |
| **Marée** | hauteur de lumière ← `Fraction` | `QuotaBrush` | ✅ `Estimated` → **grain horizontal** + waterline pointillée | `TrackBrush` (canal) |
| **Volets** | nb de volets ← `FlapRow.Fraction` | `Background` de la plaque ← `ValueBrush` | ✅ `Estimated` → `DrawingBrush` **`GrainHatch`** tuilé | `OffBrush` |

**Trois conclusions actionnables :**

1. **M-plancher = texture.** Elle est déjà implémentée dans 4 styles sur 5, bindée sur `IsEstimated`, qui
   depuis la phase 19 vaut exactement « plancher » en production. **Le travail restant se réduit aux
   Anneaux** : un `StrokeDashArray` sur les `RingArc` de valeur. C'est le canal de Bertin *grain/texture*,
   strictement orthogonal à la longueur (temps) et à la luminance (quota) : la loi d'encodage est respectée
   sans inventer de troisième variable, puisque la texture est la **quatrième** et qu'elle était libre.
2. **M-âge ne peut pas être une texture** (le canal est pris par M-plancher) ni une luminance (pris par le
   quota) ni une géométrie (pris par le temps). Il reste : **position** (une pastille dans la couche racine)
   ou **forme/glyphe**. Les deux options sont chiffrées ci-dessous.
3. **M-indispo est à moitié livrée** : `Utilization = null` ⇒ `ArcBrush(null)` = `Neutre` (arc visible mais
   neutre) et `UtilizationText = ""`. Manque **le mot** — `DataUnavailable` est calculé et bindé nulle part.
   Attention : l'absence de chiffre est déjà ambiguë avec l'état « en attente » (`HasData=false` → `WaitFill`
   dans les 4 contrôles). Le mot lève l'ambiguïté ; sans lui, EXA-03 n'est pas satisfaite.

#### M-âge : les deux options, chiffrées

**Option A — pastille dans la couche racine (RECOMMANDÉE).**

Le `Grid` racine de `MainWindow.xaml` porte déjà, en derniers enfants, les 3 pastilles d'authentification, avec
ce commentaire (vérifié dans le fichier) : *« DERNIERS enfants du Grid racine => au-dessus de TOUS les styles
de cadran : uniques pour les 5 styles et les 2 modes, sans duplication »*. C'est **la seule couche
style-agnostique du projet**, et elle est déjà éprouvée et testée (`MonterPastille`).

- Coût : **1 implémentation**, valide pour 5 styles × 2 modes × 9 thèmes automatiquement.
- Thèmes : la pastille utilise `{DynamicResource TexteSecondaire}`, que `ChronosTheme.BrushTokens()` écrase
  par thème → correcte dans les 9 thèmes **sans travail supplémentaire**.
- Test : `FindName` + `Visibility` résolue, motif `MonterPastille` déjà écrit.
- **Limite assumée :** une pastille est **globale**, alors que `Provenance` est **par fenêtre**. Voir ci-dessous.

**Option B — marque typographique accolée au pourcentage.**

`UtilizationText` est le seul canal **par fenêtre** déjà bindé dans les **5** styles (vérifié : `FiveHour.
UtilizationText` / `SevenDay.UtilizationText` apparaissent dans `MainWindow.xaml`, `CadranBraisesView`,
`CadranFusibleView`, `CadranMareeView`, `CadranVoletsView`). Étendre `PercentFormatter` suffirait.

- Coût : **1 méthode**, 0 XAML.
- **Mais** le critère de succès n° 1 dit *« sans lire de texte »*. Un glyphe préfixe reste à la limite de
  l'acceptable (« ≥ » y est déjà), un second glyffe empilé (« ≥ … 80 % ») devient du charabia.

**Arbitrage recommandé : A pour M-âge, avec la portée assumée et documentée.**

La question « M-âge doit-elle être par fenêtre ? » mérite d'être tranchée franchement, parce qu'un test
existant (`Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre`) impose déjà la portée par fenêtre
**pour la marque du pourcentage**. Réponse :

- **M-plancher reste PAR FENÊTRE** — elle l'est déjà (« ≥ » + texture, tous deux bindés par fenêtre). Le test
  existant continue de passer sans retouche. ✅
- **M-âge peut être GLOBALE**, et c'est défendable : l'âge n'est pas une propriété de la fenêtre, c'est une
  propriété du **pipeline**. Les deux fenêtres viennent de la même chaîne de sources ; si le relevé 5 h a
  trois heures, c'est que la chaîne est muette — le hebdo l'est aussi, et le diagnostic à poser est le même.
  Règle de composition honnête : **afficher le plus vieux des deux**, jamais le plus jeune (une synthèse
  conservatrice n'est pas un mensonge ; une synthèse optimiste en serait un). Le détail par fenêtre
  (source + âge) est porté par l'**infobulle** de la pastille — ce qui sert EXA-06 au cadran, en plus du
  diagnostic.

> Si le planner juge la portée globale insuffisante, la solution par fenêtre existe (option B, ou 2 pastilles),
> mais elle doit alors être **explicitement** motivée par un scénario où les deux fenêtres divergent en âge.
> La recherche n'en a trouvé aucun dans le pipeline actuel.

#### Où poser la pastille : la géométrie est déjà contrainte, et il y a une collision latente

**Découverte non listée dans le CONTEXT** : les pastilles `PastilleHorsLigne` (Ellipse 10 px, `Margin="0,0,8,8"`)
et `PastilleInvitationConnexion` (Button 14 px, `Margin="0,0,6,6"`) **peuvent être visibles en même temps et
se superposent**. Preuve par lecture de `MajPastilles` :

```
AfficherPastilleDeconnexion = _etatAuth == Deconnecte;
AfficherPastilleHorsLigne   = _etatAuth == HorsLigne;
AfficherInvitationConnexion = _jamaisDExactEtRienAAfficher && !AfficherPastilleDeconnexion;
```

`HorsLigne` ⇒ `Deconnexion = false` ⇒ l'invitation peut valoir `true`. Les deux occupent le même coin. C'est la
dette n° 9 de la phase 19 (« la cohabitation visuelle des trois sur 170 px n'a jamais été éprouvée »), et la
phase 20 y ajouterait une **quatrième** pastille.

**Correction prescrite : remplacer les 3 pastilles libres par un `StackPanel Orientation="Horizontal"`
aligné bas-droite**, contenant les 4 pastilles. Le non-recouvrement devient structurel, et le test de
non-empiètement existant reste valable.

Vérification géométrique (l'élément le plus externe est `TickRing Radius=68` + `StrokeThickness=3.2` ;
le test existant exige une distance au centre `> 71,5 px`) :

| Disposition | Coin gauche de la rangée | Distance au centre (85,85) | Verdict |
|---|---|---|---|
| 4 pastilles de 14 px, écart 4 px, marge 6 px | x = 170 − 6 − 68 = **96**, y ≈ **157** | √(11² + 72²) = **72,8 px** | ✅ > 71,5 |
| 5 pastilles de 14 px, écart 4 px | x = **78**, y ≈ 157 | √(7² + 72²) = 72,3 px | ✅ mais marge fondante |

**Quatre pastilles tiennent. Cinq sont la limite.** À documenter dans le plan comme borne dure.

### Question 3 — `IsStale` (2 min) vs `LimiteAge` (6 min) : laquelle survit ?

**Réponse : `LimiteAge` survit. `IsStale` ne survit pas — pas même renommée. Et surtout, le ViewModel cesse
de calculer une ancienneté.**

Le point n'est pas « laquelle des deux valeurs est la bonne ». Le point est **qui a l'autorité**. La doctrine
décide de la provenance ; un ViewModel qui recalcule sa propre notion de « périmé » rouvre exactement
l'incohérence que ce milestone ferme. La bonne forme n'est donc pas d'aligner 2 min sur 6 min, c'est de
**supprimer le calcul concurrent**.

Faits vérifiés :

| Fait | Preuve |
|---|---|
| `LimiteAge` = **6 minutes exactement** | `RateLimitHeaderUsageProvider.CadenceNominale = TimeSpan.FromSeconds(300)` + `TimeSpan.FromSeconds(60)` |
| `LimiteAge` est **dérivée**, jamais recopiée | Test `La_limite_d_age_est_DERIVEE_de_la_cadence_de_la_sonde` (13ᵉ test de `DoctrineFraicheurTests`) |
| `MainViewModel.IsStale` (ligne 396) dérive de `CapturedAt` ← `snap.SourceCapturedAt` | lu |
| `UsageSnapshot.SourceCapturedAt` est déclaré **LEGACY** par la phase 19, avec mention explicite « La phase 20 les retire » | commentaire XML du champ |
| `IsStale` n'a **1 seul** consommateur : son propre test | `MainViewModelTests.IsStale_vrai_quand_la_capture_depasse_deux_minutes` |

**Prescription :**

1. Supprimer `MainViewModel.IsStale` **et** `MainViewModel.CapturedAt` (ce dernier n'a que deux usages :
   la ligne qui l'alimente et la ligne `IsStale`).
2. Le remplacer par un signal **rapporté**, pas calculé, sur `WindowGaugeViewModel` :
   `EstDate = s.Provenance is EncoreValide or PlancherAvecActivite`. Aucune comparaison d'horodatage dans le
   ViewModel — c'est la garantie structurelle qu'un second seuil ne peut pas réapparaître.
3. **Conserver `UsageSnapshot.SourceCapturedAt`** : il est encore asserté par 6 tests de providers
   (`ChronosOAuthUsageProviderTests`, `ClaudeOAuthUsageProviderTests`, `ClaudeUsageObjectProviderTests`,
   `CompositeUsageProviderTests`) et sa suppression coûterait des tests pour zéro exigence. Amender son
   commentaire : il n'est plus « en attente de retrait », il est le staleness **de source**, distinct de la
   provenance **de fenêtre**.
4. Ajouter une garde de non-retour (motif `GardesDoctrineTests`, chemin par `CheminSourcesChronos`) :
   **aucun `TimeSpan.From…` comparé à une soustraction d'horodatage dans `ViewModels/`**. Sans elle, la
   contradiction repoussera au prochain refactor.

> **Ne PAS faire dans cette phase :** descendre `LimiteAge` vers 2 min via une lecture en queue de fichier.
> C'est une optimisation d'E/S (la passe coûte 2,7–3,2 s / 1,2 Go mesurés) qui ne change **rien** à
> l'honnêteté visible, et la contradiction se résout en supprimant une notion, pas en égalisant deux
> nombres. → Différé.

### Question 4 — D'où vient le NOM de la source ? (EXA-06)

**Réponse : un nouvel enum `SourceUsage` dans `Models/`, porté par une propriété `init` nullable de
`WindowState`. Coût mesuré : 0 site de construction à retoucher, 0 garde cassée.**

Les trois objections du CONTEXT tombent, chacune sur une vérification :

| Objection | Vérification | Verdict |
|---|---|---|
| « ne pas casser les 39 sites `new WindowState` » | `WindowState` n'a que **deux** membres `required` : `Kind` et `Reliability`. Tous les sites sont des initialiseurs d'objet. Compte réel : **6 en `src/`, 42 en `tests/`** | ❌ objection infondée : une propriété `init` nullable de plus casse **0 site sur 48** |
| « ne pas casser la recomposition du composite (garde nominative) » | La garde `Toute_propriete_de_UsageSnapshot_est_nommee_…` réfléchit `typeof(**UsageSnapshot**).GetProperties(...)`. Elle ne regarde **jamais** `WindowState` | ❌ objection infondée, **à condition de poser le champ sur `WindowState` et non sur `UsageSnapshot`** |
| « le composite reconstruit par `new`, pas par `with` » | `Best()` rend `primary` ou `fallback` — **l'instance, par référence**. Le commentaire de `WindowState.StatutServeur` le dit mot pour mot : *« ce champ voyage gratuitement à travers toute la chaîne de composites »* | ✅ c'est précisément pourquoi il faut le poser là |

**Les cinq membres, calqués sur la chaîne DI réelle** (`App.xaml.cs`, lignes 347-357, ordre externe → interne) :

| Membre | Producteur | Libellé FR (diagnostic) |
|---|---|---|
| `SondeEnTetes` | `RateLimitHeaderUsageProvider` | sonde d'en-têtes de rate-limit |
| `EndpointOAuthChronos` | `ChronosOAuthUsageProvider` | endpoint OAuth (login Chronos) |
| `EndpointOAuthClaude` | `GatedOAuthUsageProvider` → `ClaudeOAuthUsageProvider` | endpoint OAuth (jeton app bureau / CLI) |
| `PontStatusLine` | `ClaudeUsageObjectProvider` | pont statusLine Claude Code |
| `MagasinDernierExact` | `LastExactStore.Reconstruire` | dernier exact persisté |

> La ROADMAP en nomme 5 en fusionnant les deux endpoints OAuth et en comptant « dernier exact + delta »
> comme une source. **Les deux endpoints sont deux providers distincts** (l'un lit `oauth.dat`, l'autre le
> coffre de l'app bureau) : les fusionner rendrait le diagnostic incapable de dire lequel des deux répond —
> c'est-à-dire de faire exactement son travail. Et **« dernier exact + delta » n'est pas une source, c'est un
> ÉTAT** (`ProvenanceReleve.PlancherAvecActivite`) appliqué à l'une des 5 sources. Le diagnostic doit donc
> rendre le **couple** (source, provenance) : « sonde d'en-têtes, il y a 12 min → plancher ».

**Câblage, 4 points seulement :**

1. `Models/SourceUsage.cs` — l'enum (5 membres, pas de `Inconnue` : `null` porte déjà « non renseigné », et
   le projet proscrit les affirmations nées d'une absence).
2. `WindowState.Source { get; init; }` — nullable, documentée sur le modèle de `StatutServeur`/`Provenance`.
3. Les 5 producteurs la posent (**5 lignes**).
4. `DoctrineFraicheur` : `Qualifier` utilise `candidat with { … }` → la source du candidat gagnant est héritée
   **automatiquement**, y compris quand le magasin prend la main ; `Indisponible` doit poser
   `Source = null` à côté de `Provenance = null` (cohérence : une fenêtre indisponible n'est alimentée par
   personne).
5. Le vocabulaire FR dans **`Chronos.Text`** (un seul mapping, réutilisé par `DiagnosticService` **et** par le
   ViewModel pour l'infobulle) — c'est le dossier déjà consacré au « formatage FR pur, aucune dépendance WPF,
   aucun I/O » (`PercentFormatter`, `TokenFormatter`, `CountdownFormatter`).

### Question 5 — La dette de durée des tests : levable, et à quel prix ?

**Réponse : levable, et le prix est de 0 site de construction — la mesure invalide l'estimation qui la
bloquait.**

#### Mesures (2026-09-12, machine cible, `dotnet test --no-build -c Debug`)

| Mesure | Résultat |
|---|---|
| Suite **hors** `DiagnosticServiceTests` | **692 tests / 2 s** |
| `DiagnosticServiceTests` **seul** | **7 tests / 2 min 8 s** (real 2 m 10) |
| Part de la suite imputable à 7 tests | **98,4 %** |
| Coût moyen d'un `BuildReportAsync` | **≈ 18,3 s** |

#### Décomposition d'un `BuildReportAsync` (sonde hors dépôt, réplique exacte du code)

| Section | Durée | Part |
|---|---|---|
| **A. `FindTokenVaults` (2 racines, profondeur 3)** | **17 703 ms** | **94,4 %** |
| E. Poll UIA réel (`DesktopUiaSessionSource`) | 936 ms | 5,0 % |
| D. `SessionMonitor.Read` (transcripts) | 39 ms | 0,2 % |
| C. Comptage `*.jsonl` (839 fichiers) | 7 ms | < 0,1 % |
| B. Cartographie des dossiers Claude | 2 ms | < 0,1 % |

Le diagnostic du CONTEXT (« balaie `%APPDATA%`/`%LOCALAPPDATA%` **et** fait un poll UIA ») est exact, mais la
**pondération** ne l'était pas : le poll UIA est du bruit, la recherche de coffres est la totalité du
problème. **Une seule couture suffit.**

#### La forme la moins invasive

Les phases 17 et 18 ont établi le protocole, littéralement dans ce fichier :

> *« OPTIONNEL et en dernière position à dessein : les 10 sites de construction préexistants (1 en production,
> 9 en tests) compilent sans retouche. »*

Appliquer le même protocole une troisième fois ⇒ **aucun des 10 sites ne change**. L'hypothèse qui gelait
cette dette depuis deux phases (« changer la signature coûte 10 sites ») est fausse pour un paramètre
optionnel terminal.

**Prescription :**

```csharp
public DiagnosticService(IClaudeTokenReader tokenReader, ChronosPaths paths,
                         SettingsService settings, IUsageProvider composite, IClock clock,
                         IAuthStatus? authStatus = null, IEtatServeur? etatServeur = null,
                         IInventaireMachine? machine = null)   // ← 3e paramètre optionnel terminal
```

`IInventaireMachine` (dans `Services/`, type **neutre** — aucun type WPF) expose ce que la recherche a mesuré
comme cher, et **rien d'autre** :

```csharp
public interface IInventaireMachine
{
    IReadOnlyList<string> CoffresOAuth(string racine);      // 17,7 s → 0 en test
    IReadOnlyList<SessionSnapshot> SessionsBureau(DateTimeOffset now);  // 0,94 s → 0 en test
}
```

- `null` ⇒ implémentation réelle (production inchangée, aucune régression de comportement).
- Les 7 tests passent un faux rendant des listes vides. **Aucune des 7 classes de test n'asserte le contenu
  de ces deux sections** (vérifié ligne à ligne) : la substitution ne change **aucune assertion existante**.
- Gain attendu : **7 × 18,3 s → ≈ 0 s**, suite complète ramenée de **2 min 10 s à quelques secondes**.

**Alternative écartée, à mentionner pour qu'elle ne revienne pas :** mémoïser `FindTokenVaults` en statique.
Gain réel (le 1ᵉʳ test paie, les 6 autres non : ≈ 128 s → ≈ 19 s) pour 5 lignes. **Mais un diagnostic qui met
en cache un balayage de l'environnement est un diagnostic qui peut mentir** au second usage — précisément le
défaut que ce milestone éradique. À refuser.

### Question 6 — Le trou de la `UniformGrid` des réglages

**Réponse : la piste du CONTEXT est la bonne, mais elle exige de remplacer `UniformGrid` par `Grid` —
`UniformGrid` ignore `Grid.ColumnSpan`.** Et la mesure confirme que `Columns="3"` tronquerait.

État actuel (`SettingsWindow.xaml`, lignes 327-334) : `UniformGrid Columns="2"` + 3 boutons ⇒
ligne 0 = [Recalibrer hebdo…][Source terminal], ligne 1 = [Diagnostic…][**vide**].

#### Mesures (WPF `FormattedText`, Segoe UI, `FontSize=12`, 96 dpi)

| Libellé | Largeur du texte |
|---|---|
| « Recalibrer hebdo » + « … » | 98,5 px + ≈ 9 px = **≈ 107 px** |
| « Source terminal » | **83,0 px** |
| « Diagnostic… » | ≈ **74 px** |

Largeur utile du panneau : `330 − 2×16 (Padding) − 2×1 (BorderThickness)` = **296 px**.
Style `Flat` : `Padding="10,7"` ⇒ 20 px consommés horizontalement. Marges XAML : 4 px.

| Colonnes | Largeur cellule | Largeur utile pour le texte | « Recalibrer hebdo… » (107 px) |
|---|---|---|---|
| 2 | 148,0 px | **124 px** | ✅ tient (17 px de marge) |
| **3** | **98,7 px** | **74,7 px** | ❌ **dépasse de ≈ 32 px → troncature** |

**La crainte du CONTEXT est confirmée par la mesure.** `Columns="3"` est à exclure.

**Second défaut, non signalé jusqu'ici : les marges sont fausses par rapport aux positions réelles.**

| Bouton | Position réelle en `Columns=2` | `Margin` actuelle | Devrait être |
|---|---|---|---|
| Recalibrer hebdo… | ligne 0, col. **gauche** | `0,0,4,6` | ✅ correct |
| Source terminal | ligne 0, col. **droite** | `0,0,4,0` ❌ | `4,0,0,6` |
| Diagnostic… | ligne 1, col. **gauche** | `4,0,0,0` ❌ | `0,0,4,0` |

**Prescription :** remplacer la `UniformGrid` par une `Grid` 2 colonnes × 2 lignes, `Diagnostic…` en
`Grid.Row="1" Grid.ColumnSpan="2"`, et corriger les trois marges. Le bouton pleine largeur du bas est
cohérent avec le « Quitter Chronos » qui le suit immédiatement (pleine largeur lui aussi).

> **Piège WPF à écrire dans le plan :** `UniformGrid` **ignore** `Grid.Row` / `Grid.Column` /
> `Grid.ColumnSpan` — elle place ses enfants dans l'ordre, en remplissant ligne par ligne. Poser un
> `ColumnSpan` sur un enfant d'`UniformGrid` compile, s'affiche sans erreur, et **ne fait rien**. C'est le
> genre de bug qu'aucun `dotnet build` n'attrape et que seul un test BAML avec `Measure/Arrange` voit.

### Question 7 — `TokensText` / `HasTokens` / `DataUnavailable` / `EstimatedTokens`

**Réponse, propriété par propriété — aucune ne reste dans l'état actuel.**

| Propriété | Décision | Justification |
|---|---|---|
| `DataUnavailable` (`MainViewModel`) | **BINDER** | EXA-03 exige l'état « indisponible » lisible. Le mot est le seul moyen de le distinguer de l'état « en attente » (`HasData=false`), qui produit déjà un visuel neutre dans les 4 contrôles alternatifs. **Sans ce binding, EXA-03 n'est pas satisfaite.** |
| `TokensText` / `HasTokens` (`WindowGaugeViewModel`) | **BINDER, mais EN INFOBULLE / dans le diagnostic — pas en ligne permanente** | C'est la matière brute de DEL-04 (amendement du « + delta estimé »), elle doit devenir visible. Mais la v1.3 a **délibérément** retiré la ligne de tokens du centre (« centre épuré ») : la réinstaller en permanence sur 170 px reviendrait à annuler une décision de design pour satisfaire une exigence qui ne le demande pas. **La marque (« ≥ » + texture) est permanente ; la matière brute est à la demande.** |
| `EstimatedTokens` (`WindowState`) | **SUPPRIMER** | Mort en production (aucun chemin de présentation ne le lit, garde de non-retour en place). Coût réel mesuré : **4 sites** — la déclaration, un `= null` dans `DoctrineFraicheur.Indisponible`, et **le test `EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran` qui le pilote**. ⚠️ Ce test est une **garde de non-retour** : le supprimer rouvre la porte. Deux options honnêtes : (a) garder le champ et la garde ; (b) supprimer le champ **et** remplacer la garde comportementale par une garde structurelle (« aucun champ nommé `EstimatedTokens` dans `Models/` »). **Recommandation : (b)** — un champ absent est une garantie plus forte qu'un champ mort surveillé. |
| `IsEstimated` (`WindowGaugeViewModel`) | **RENOMMER en `EstPlancher`** | Le nom ment depuis la phase 19 : `SourceReliability.Estimated` n'est plus produit que par la branche « plancher » (vérifié : 1 seul site d'affectation en `src/`). Dans une phase intitulée « honnêteté visible », laisser une propriété dont le nom désigne un concept supprimé est une contradiction interne. Coût mesuré : **12 lignes d'assertion** (`CadranBindingTests` ×3, `MainViewModelTests` ×4, `WindowGaugeViewModelTests` ×4, + 1 commentaire), **8 bindings XAML**, **1 ligne** dans `CadranPreviewViewModel`. |

**Corollaire sur les DP des contrôles :** les DP `Estimated` d'`EmberRingControl` / `FuseBar` / `TideColumn`
décrivent un **rendu**, pas une doctrine. Les renommer en `Grain` (ou `Texture`) découple proprement les deux
couches : la doctrine dit « plancher », la vue décide que « plancher → grain ». Facultatif, mais cohérent
avec le fait que la galerie de cadrans (`--cadrans`) pilote ces DP par un booléen d'aperçu sans provenance.

### Question 8 — Ce qui casse dans les 699 tests

**Aucun test ne casse « par surprise ».** Chaque rupture est la conséquence d'une décision ci-dessus, et
chacune se répare par réécriture, pas par suppression — **sauf une**, argumentée.

| # | Test | Classe | Cause | Traitement |
|---|---|---|---|---|
| 1 | `IsStale_vrai_quand_la_capture_depasse_deux_minutes` | `MainViewModelTests` | Q3 : `IsStale` disparaît | **SUPPRIMER**, et le **remplacer** par un test de non-retour : « aucun seuil d'ancienneté n'est calculé dans `ViewModels/` ». Une suppression sèche perdrait la preuve ; la substitution la déplace au bon niveau |
| 2 | `Fenetre_exacte_masque_le_badge_estimee_et_porte_utilisation_reelle` | `WindowGaugeViewModelTests` | Q7 : `IsEstimated` → `EstPlancher` | **RÉÉCRIRE** (renommage d'assertion + du nom de test, le libellé « badge estimée » est périmé) |
| 3 | `Fenetre_estimee_rallume_le_badge` | `WindowGaugeViewModelTests` | idem | **RÉÉCRIRE** |
| 4 | `UtilizationText_estime_prefixe_tilde` | `WindowGaugeViewModelTests` | surcharge historique `Format(double?, bool)` conservée pour la galerie | **CONSERVER TEL QUEL** — la surcharge reste, elle sert `CadranPreviewViewModel` |
| 5 | `Apply_derive_le_prefixe_de_la_provenance_et_non_de_la_fiabilite` | `WindowGaugeViewModelTests` | asserte `IsEstimated` (l. 104, 117) | **RÉÉCRIRE** (renommage) |
| 6-8 | `Etat_estime_…`, `Etat_indisponible_…`, `Etat_mixte_…` (asserts `IsEstimated` l. 81, 118, 119) | `CadranBindingTests` | renommage | **RÉÉCRIRE** |
| 9 | `EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran` | `CadranBindingTests` | Q7 option (b) : le champ disparaît | **REMPLACER** par une garde structurelle dans `GardesDoctrineTests` — *jamais* supprimer sans remplaçant |
| 10-11 | 2 tests assertant `Assert.True(vm.SevenDay.IsEstimated)` (recalibrage hebdo, l. 283/291) + 2 assertant `False` (l. 379/385) | `MainViewModelTests` | renommage | **RÉÉCRIRE** (4 lignes) |
| 12-13 | `Rapport_sans_token_conseille_…` et `Un_diagnostic_sans_sonde_reste_lisible` — tous deux `Assert.Contains("estimé", report)` | `DiagnosticServiceTests` | Q4 : `Describe()` change de vocabulaire (le « ~ » du diagnostic devient « ≥ », et la ligne nomme la source + l'âge) | **RÉÉCRIRE** l'assertion. ⚠️ Ces deux tests montent une fenêtre `Estimated` **sans** `Provenance` ni `Source` : le nouveau libellé doit rester correct quand les deux sont `null` |
| 14 | Les 7 tests de `DiagnosticServiceTests` | Q5 : 3ᵉ paramètre optionnel | **AUCUNE rupture de compilation** (paramètre optionnel). Ajouter le faux dans les 7 montages = 7 lignes |
| — | `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` | — | aucune | **DOIVENT rester vertes.** `GardesDoctrineTests` gagne 1 à 2 tests |
| — | `Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre` | `MainViewModelTests` | Q2 : M-plancher reste par fenêtre | **INTACT** — et il verrouille le choix de portée |

**Bilan prévisionnel :** ≈ 13 tests réécrits, 1 supprimé **avec remplaçant**, 2 gardes structurelles ajoutées,
plus les nouveaux tests d'EXA-03 / EXA-06. Le critère de sortie reste celui de la phase 19 : **0 échec + chaque
écart justifié nominativement**, pas « ≥ 699 ».

#### Contrainte de test absolue, à écrire dans chaque tâche de dessin

> **Toute nouvelle classe de test chargeant du XAML DOIT porter `[Collection("XAML WPF")]`.**
> Le chargeur BAML de WPF (`WpfXamlType.FindKnownMember`) a une course documentée sous parallélisme xUnit
> (décision 16-03) qui fait lever un `XamlParseException` **intermittent** sur un XAML parfaitement valide.
> Le symptôme dépend de la vitesse relative des classes : **indétectable par relance isolée**.
> Corollaire : **exécuter la suite complète DEUX fois** avant de conclure (protocole 19-05).
> En pratique, préférer **étendre `CadranBindingTests` / `ReglagesBindingTests`** plutôt que créer une classe.

---

## Architecture Patterns

### Structure des fichiers touchés

```
src/Chronos/
├── Models/
│   ├── SourceUsage.cs              # NOUVEAU — enum 5 membres, aucun « Inconnue »
│   └── WindowState.cs              # + Source { get; init; }  (nullable, motif StatutServeur)
├── Text/
│   ├── PercentFormatter.cs         # inchangé si M-âge = pastille ; + 1 branche si M-âge = typographique
│   └── LibelleSource.cs            # NOUVEAU — vocabulaire FR UNIQUE de SourceUsage (neutre, 0 WPF)
├── Services/
│   ├── DoctrineFraicheur.cs        # Indisponible : + Source = null
│   ├── DiagnosticService.cs        # Describe() nomme source + âge ; 3e param optionnel IInventaireMachine
│   ├── IInventaireMachine.cs       # NOUVEAU — couture des 2 sondages chers
│   ├── InventaireMachine.cs        # NOUVEAU — implémentation réelle (code déplacé, pas réécrit)
│   └── {RateLimitHeader,ChronosOAuth,ClaudeOAuth,ClaudeUsageObject}UsageProvider.cs, LastExactStore.cs
│                                   # 5 lignes : Source = SourceUsage.X
├── ViewModels/
│   ├── WindowGaugeViewModel.cs     # IsEstimated → EstPlancher ; + EstDate ; + Source/InfobulleSource
│   ├── MainViewModel.cs            # − IsStale, − CapturedAt ; + AfficherReleveDate (recomposé)
│   └── CadranPreviewViewModel.cs   # suit le renommage (1 ligne)
└── Views/
    ├── MainWindow.xaml             # StrokeDashArray (Anneaux) ; rangée de pastilles ; mot « indisponible »
    ├── SettingsWindow.xaml         # UniformGrid → Grid 2×2, ColumnSpan, 3 marges
    └── Cadrans/*.xaml              # 8 bindings renommés, aucune logique nouvelle
```

### Pattern 1 — Le signal visuel traverse par le ViewModel, jamais par un type WPF dans `Services/`

`ServicesLayerPurityTests` interdit tout type WPF dans `Services/` et `Models/`. La chaîne honnête est donc :

```
DoctrineFraicheur (pur)  →  WindowState.Provenance/.Source (enum, pur)
                         →  WindowGaugeViewModel.EstPlancher / .EstDate (bool, ObservableProperty)
                         →  XAML : Visibility / StrokeDashArray / DP Grain
```

Le XAML lit des **booléens**, jamais un enum via un converter. C'est le motif déjà établi par le dépôt :
*« Deux booléens plutôt qu'un enum bindé : cohérent avec IsStyleArcs / IsModeNormal, zéro converter »*
(commentaire de `AfficherPastilleDeconnexion`).

### Pattern 2 — La couche racine du `Grid` : un signal, cinq styles, deux modes, neuf thèmes

```xml
<!-- DERNIERS enfants du Grid racine => au-dessus de TOUS les styles. -->
<StackPanel Orientation="Horizontal"
            HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,6,6">
    <!-- 4 pastilles max (mesure : coin gauche à x=96 ⇒ 72,8 px du centre > seuil 71,5) -->
</StackPanel>
```

Règles dérivées du code existant, à ne pas enfreindre :
- **Actionnable ⇒ `Button`** (et non `Ellipse`) : `ButtonBase` marque `MouseLeftButtonDown` comme `Handled`,
  donc aucun `DragMove` parasite.
- **`Background` non-null** pour rester hit-testable sur `AllowsTransparency` ; `Transparent` suffit,
  seul `{x:Null}` ne l'est pas.
- **Informatif ⇒ `Ellipse` inerte**, token `TexteSecondaire` : ne jamais crier « répare-moi » pour un fait
  qui n'appelle aucun geste. **M-âge est informative.**
- **`Command="{Binding ReconnecterCommand}"` uniquement.** Jamais `LoginClaudeCommand`.
- Les états s'excluent dans **une seule méthode de recomposition** du ViewModel (motif `MajPastilles`), jamais
  par un empilement de `Visibility` indépendantes.

### Pattern 3 — Le vocabulaire FR est calculé UNE fois

Précédent explicite dans `WindowGaugeViewModel.LibelleStatut` : *« MainViewModel réutilise ces textes déjà
calculés plutôt que de refaire un second mapping — deux mappings divergeraient le jour où le vocabulaire du
serveur bougera. »* Le libellé de `SourceUsage` obéit à la même règle : **un seul point**, dans `Chronos.Text`,
consommé par `DiagnosticService` **et** par l'infobulle du ViewModel.

### Anti-patterns à refuser explicitement

- **Un converter enum → Brush.** Le dépôt a déjà migré `UtilizationToBrushConverter` (statique) vers
  `ChronosTheme.ArcBrush` (live, suit les 9 thèmes). Recréer un converter statique réintroduirait un chemin
  de couleur qui ignore le thème.
- **Une couleur en dur pour une nouvelle marque.** Voir Piège 1.
- **Un `Storyboard` / `BlurEffect` / `DropShadowEffect` sur `MainWindow`.** `AllowsTransparency=True` ⇒ rendu
  logiciel ; le tick de 1 s rendrait le coût permanent. (`SettingsWindow` en a un — elle n'est ouverte qu'à
  la demande, c'est différent.)
- **Aligner `IsStale` sur 6 min** au lieu de le supprimer. Deux notions dont l'une recopie l'autre est le
  même défaut avec un chiffre différent.
- **Un réglage utilisateur pour la limite d'âge.** *« Un seuil d'honnêteté exposé à l'utilisateur est un
  seuil qui finit désactivé »* — précédent documenté : `FiveHourBudgetSource: "Manual"`, encore visible
  aujourd'hui dans le `settings.json` réel de cette machine, gelé depuis le 2026-07-09.

---

## Don't Hand-Roll

| Problème | Ne PAS construire | Utiliser à la place | Pourquoi |
|---|---|---|---|
| Marquer le plancher dans les 4 styles alternatifs | Un nouveau canal de rendu | **La DP `Estimated` existante** (`EmberRingControl`, `FuseBar`, `TideColumn`, `GrainHatch`) | Déjà écrite, déjà bindée, déjà à l'échelle 170 px. Post-phase-19 elle vaut déjà « plancher » |
| Arc en pointillé aux Anneaux | Un `Path` custom, une `PathGeometry` à trous | **`StrokeDashArray` sur `RingArc`** | `RingArc : Shape` ⇒ `Stroke*` hérités gratuitement, géométrie inchangée, rendu logiciel gratuit |
| Suivre les 9 thèmes | Recopier un hex par thème | **`{DynamicResource X}` + `ChronosTheme.BrushTokens()`** | Le dictionnaire est déjà écrasé à chaque `SelectTheme` ; un token nouveau suit sans code |
| Un signal unique pour 5 styles × 2 modes | 10 copies dans les `UserControl` | **Derniers enfants du `Grid` racine de `MainWindow`** | Motif déjà en production pour les 3 pastilles, avec son commentaire de justification |
| Formater un pourcentage / un compte de tokens / un compte à rebours | Un `string.Format` local | **`Chronos.Text.{PercentFormatter,TokenFormatter,CountdownFormatter}`** | Purs, testés, déterministes (culture-invariants) |
| Convertir une unité d'usage | Un `/100` local | **`UsageNormalization`** | `NormalisationUniqueTests` balaie le TEXTE source et échoue sur tout `/ 100` ailleurs |
| Localiser les sources depuis un test | `Assembly.Location`, remontée depuis `AppContext.BaseDirectory` | **`AssemblyMetadata("CheminSourcesChronos")`** | `Assembly.Location` est **vide** en mono-fichier (CLAUDE.md) |
| Rendre `DiagnosticService` testable | Un flag `bool modeTest` | **Paramètre optionnel terminal + faux** | Un flag de test dans du code de production teste un autre code que celui qui tourne |

**Key insight :** dans cette phase, presque tout le « travail » consiste à **brancher ce qui existe**. Chaque
fois qu'une tâche du plan crée un nouveau mécanisme, il faut d'abord vérifier qu'un mécanisme équivalent
n'est pas déjà en place et simplement non bindé — c'est le cas pour 4 des 5 styles, pour les trois pastilles,
et pour la moitié de l'état « indisponible ».

---

## Common Pitfalls

### Piège 1 — « Lisible dans les thèmes » est déjà à moitié faux, et la mesure n'est pas 3

**Ce qui cloche :** le CONTEXT annonce 3 thèmes ; `ThemeCatalog.All` en compte **9**
(minuit, ardoise, nord, néon, aurore, ambre, moka, roseraie, forêt) et `ThemingTests` l'asserte.
Pire : **les 4 `UserControl` de cadran codent leurs couleurs en dur** — `#F4F2EC`, `#C7C6D0`, `#A9A8B2`,
`#211D2A`, `#161019`, `#55000000` — et ne suivent le thème que par `ValueBrush`. Un thème clair rendrait déjà
ces textes illisibles aujourd'hui.

**Pourquoi ça arrive :** la refonte visuelle a livré les 4 pistes avec une palette de maquette figée.

**Comment l'éviter :** **toute marque nouvelle utilise un `DynamicResource` de `DesignTokens.xaml`, jamais un
hex.** Corollaire : ne pas poser la marque dans les `UserControl` (qui sont en hex), mais dans la couche
racine (qui est en `DynamicResource`) — ce qui converge avec le Pattern 2.

**Signes d'alerte :** un `#` dans un diff de la phase 20 hors `DesignTokens.xaml` / `ChronosTheme.cs`.

### Piège 2 — `CadranBindingTests` lit le **vrai** `%APPDATA%\Chronos\settings.json`

**Ce qui cloche :** `CadranBindingTests.BuildWindow` construit ses dépendances avec `ChronosPaths.Default()`.
Le `MainViewModel` appelle `settings.Load()` dans son constructeur ⇒ **`CadranStyle`, `CadranMode` et
`ThemeKey` viennent de la machine de l'utilisateur.** État réel constaté aujourd'hui :
`CadranStyle: "Arcs"`, `CadranMode: "Normal"`, `ThemeKey: "ardoise"`.

**Pourquoi ça arrive :** ces tests sont antérieurs à la convention `TempPaths()` qu'applique
`ReglagesBindingTests` (qui, lui, asserte même `Assert.StartsWith(Path.GetTempPath(), dir)`).

**Conséquence pour cette phase :** un `[WpfFact]` qui vérifierait « la texture du plancher est visible dans le
style Braises » serait **vert par accident** ici et **rouge sur une autre machine** (ou après un simple clic
dans les réglages de l'utilisateur).

**Comment l'éviter :** tout nouveau test de style **pose explicitement** `vm.CadranStyle = CadranStyle.X` et
`vm.IsModeEtendu = …` après construction, **ou** passe par `TempPaths()`. Ne jamais dépendre du persisté.
Idéalement, migrer `BuildWindow` vers `TempPaths()` — mais c'est une retouche transverse à 14 tests, à peser.

### Piège 3 — `UniformGrid` ignore silencieusement `Grid.ColumnSpan`

Voir Question 6. Compile, s'affiche, ne fait rien. Seul un test BAML avec `Measure`/`Arrange` le voit.

### Piège 4 — Un binding qui ne s'évalue jamais rend un test vert pour une mauvaise raison

**Ce qui cloche :** une `Window` jamais affichée n'a pas de template appliqué ; son `Content` n'a **aucun
parent visuel**, donc le `DataContext` ne se propage pas : `Command` reste `null` et `Visibility` reste à son
défaut `Visible`.

**Comment l'éviter :** le dépôt a déjà la parade, deux fois (`MonterPastille`, `MonterReglages`) :
1. poser le `DataContext` sur la **racine du contenu** (les enfants d'un `Panel` sont bien des enfants visuels) ;
2. **purger la file du Dispatcher** — la réévaluation déclenchée par un changement de `DataContext` est
   **différée** : `racine.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle)` ;
3. puis `Measure` / `Arrange` sur l'empreinte réelle (170×170).

Sauter l'étape 2 est l'erreur classique — et elle produit un vert.

### Piège 5 — Marquer « exact daté » comme dégradé serait une régression d'honnêteté

**Ce qui cloche :** l'intuition « vieux = moins sûr » est fausse ici. `EncoreValide` signifie que la doctrine
est **allée vérifier** (`journal.Covers(t)` et `!activite.HasActivity`) et a **prouvé** que l'utilisation n'a
pas bougé. Griser, voiler ou texturer ce chiffre dirait au lecteur « doute de moi » alors que c'est le seul
état où le système a fourni une preuve positive.

**Comment l'éviter :** M-âge est **informative** et **additive** (elle s'ajoute à côté du chiffre), jamais
**soustractive** (elle ne retire ni luminance, ni saturation, ni netteté au chiffre lui-même). C'est exactement
la distinction que le dépôt a déjà faite entre la pastille « déconnecté » (actionnable, `Alerte`) et la
pastille « hors ligne » (informative, `TexteSecondaire`, inerte).

### Piège 6 — Deux pastilles peuvent déjà se superposer

Voir Question 2. `HorsLigne` + `Invitation` sont simultanément possibles et occupent le même coin
(`Margin="0,0,8,8"` 10 px vs `Margin="0,0,6,6"` 14 px). La phase 20 y ajoute une quatrième pastille : la
rangée `StackPanel` n'est pas un embellissement, c'est la correction d'un défaut existant.

### Piège 7 — Le diagnostic dit encore « ~ » là où le cadran dit « ≥ »

`DiagnosticService.Describe` (l. 590) rend `"estimé — ~X %"` pour `SourceReliability.Estimated`, c'est-à-dire
pour un plancher. Le cadran, lui, dit `"≥ X %"` depuis la phase 19. **Deux vocabulaires pour le même fait** —
et le tilde véhicule une incertitude **symétrique** que 19-04 a explicitement rejetée (*« un tilde dirait
"autour de 80", ce qui autoriserait la lecture "peut-être 75" »*). À corriger dans la même tâche qu'EXA-06,
en sachant que 2 tests assertent littéralement le mot « estimé ».

---

## Code Examples

### Exemple 1 — Le plancher aux Anneaux (le seul style qui n'a pas le canal texture)

```xml
<!-- MainWindow.xaml — arc de valeur hebdo. StrokeDashArray hérité de Shape : aucune géométrie
     nouvelle, aucun effet, donc gratuit sous rendu logiciel (AllowsTransparency).
     Le pointillé est la marque du PLANCHER (borne inférieure), jamais celle de l'âge :
     un exact daté mais vérifié reste un trait PLEIN. -->
<ctrl:RingArc x:Name="ArcHebdo" Radius="38" StrokeThickness="8"
              StrokeStartLineCap="Round" StrokeEndLineCap="Round"
              Fraction="{Binding SevenDay.FractionElapsed}"
              Stroke="{Binding SevenDay.ValueBrush}">
    <ctrl:RingArc.Style>
        <Style TargetType="ctrl:RingArc">
            <Style.Triggers>
                <DataTrigger Binding="{Binding SevenDay.EstPlancher}" Value="True">
                    <!-- Unités de StrokeDashArray = multiples de StrokeThickness (8 px ici). -->
                    <Setter Property="StrokeDashArray" Value="0.55 0.45"/>
                    <Setter Property="StrokeDashCap" Value="Round"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </ctrl:RingArc.Style>
</ctrl:RingArc>
```

> **Piège d'unité :** `StrokeDashArray` s'exprime en **multiples de `StrokeThickness`**, pas en pixels.
> Avec `StrokeThickness="8"`, `Value="0.55 0.45"` donne des tirets de 4,4 px espacés de 3,6 px — comparable
> au grain de `EmberRingControl` (`DashStyle{1.4, 1.4}` sur un `Pen` de 1,4). Une valeur en pixels
> (« 4 3 ») produirait des tirets de 32 px : l'anneau paraîtrait cassé.

### Exemple 2 — Le champ de source, posé au bon endroit

```csharp
// Models/WindowState.cs — MÊME motif, MÊME justification que StatutServeur et Provenance.
/// <summary>Phase 20 (EXA-06) — QUI a produit ce chiffre. Orthogonal à <see cref="Provenance"/>, qui dit
/// ce qu'on a VÉRIFIÉ : un plancher a une source (celle du relevé mémorisé) ET un état (« borne
/// inférieure »). Les fusionner produirait un enum produit cartésien.
///
/// Porté par WindowState et NON par UsageSnapshot, pour la raison mécanique déjà établie deux fois :
/// Best() rend l'INSTANCE gagnante PAR RÉFÉRENCE, donc ce champ traverse gratuitement les trois
/// composites imbriqués, là où un champ de snapshot serait détruit par le « new » de la recomposition
/// (et la garde Toute_propriete_de_UsageSnapshot_… ne surveille QUE UsageSnapshot).
///
/// null = non renseigné. Pas de membre « Inconnue » : une absence ne doit jamais produire d'affirmation.</summary>
public SourceUsage? Source { get; init; }
```

```csharp
// Services/DoctrineFraicheur.cs — Qualifier hérite la source du candidat gagnant (« candidat with »),
// y compris quand le magasin prend la main : AUCUNE ligne à ajouter dans Qualifier.
// Seul Indisponible doit l'effacer, à côté de Provenance :
private static WindowState Indisponible(WindowState vivante)
    => vivante with
    {
        Reliability = SourceReliability.Unavailable,
        Utilization = null,
        TokensDepuisReleve = null,
        Provenance = null,
        Source = null,        // ← une fenêtre indisponible n'est alimentée par personne
    };
```

### Exemple 3 — EXA-06 : la ligne du diagnostic qui répond à l'exigence

```csharp
// Services/DiagnosticService.cs — Describe() rend enfin le COUPLE (qui, quand) + l'état.
// Vocabulaire FR emprunté à Chronos.Text : un seul mapping pour tout le projet.
private string Describe(WindowState w)
{
    var baseTexte = w.Reliability switch
    {
        SourceReliability.Exact     => "EXACT — " + Pourcentage(w),
        SourceReliability.Estimated => "PLANCHER — ≥ " + Pourcentage(w),   // « ≥ » et non « ~ » (19-04)
        _                           => "indisponible",
    };

    // EXA-06, les deux moitiés : QUI alimente, et DEPUIS QUAND.
    baseTexte += " · source : " + LibelleSource.Format(w.Source);          // null → « non renseignée »
    baseTexte += " · relevé " + LibelleSource.Anciennete(w.CapturedAt, _clock.UtcNow);
    if (w.Provenance is { } p) baseTexte += " · " + LibelleSource.Provenance(p);

    if (w.StatutServeur is { } st) baseTexte += " · serveur : " + LibelleStatutServeur(st);
    if (w.Depassement?.Utilization is { } d)
        baseTexte += " · dépassement " + UsageNormalization.PourcentagePourAffichage(d);

    return baseTexte;
}
```

Rendu attendu, les quatre apparences :

```
5 h   : EXACT — 42 % · source : sonde d'en-têtes de rate-limit · relevé il y a 2 min · frais
5 h   : EXACT — 42 % · source : dernier exact persisté · relevé il y a 3 h 12 · encore valide (aucune activité depuis)
5 h   : PLANCHER — ≥ 42 % · source : dernier exact persisté · relevé il y a 3 h 12 · borne inférieure (activité depuis)
Hebdo : indisponible · source : non renseignée · relevé : date de capture inconnue
```

### Exemple 4 — La couture qui rend la suite 60× plus rapide

```csharp
// Services/IInventaireMachine.cs — type NEUTRE (aucun using WPF) : ServicesLayerPurityTests reste vert.
/// <summary>Les DEUX sondages d'environnement MESURÉS comme chers dans BuildReportAsync
/// (2026-09-12, machine cible) : recherche de coffres 17 703 ms, poll UIA 936 ms — 99,4 % du coût
/// d'un rapport. Tout le reste du rapport coûte moins de 50 ms cumulés et n'a pas besoin de couture.</summary>
public interface IInventaireMachine
{
    IReadOnlyList<string> CoffresOAuth(string racine);
    IReadOnlyList<SessionSnapshot> SessionsBureau(DateTimeOffset now);
}
```

```csharp
// Signature : 3e paramètre OPTIONNEL en DERNIÈRE position — protocole d'extension déjà appliqué deux fois
// dans ce même fichier (authStatus, phase 17 ; etatServeur, phase 18). Les 10 sites de construction
// préexistants compilent SANS UNE RETOUCHE. Mesuré, pas supposé.
public DiagnosticService(IClaudeTokenReader tokenReader, ChronosPaths paths,
                         SettingsService settings, IUsageProvider composite, IClock clock,
                         IAuthStatus? authStatus = null, IEtatServeur? etatServeur = null,
                         IInventaireMachine? machine = null)
{
    …
    _machine = machine ?? new InventaireMachine();   // production strictement inchangée
}
```

---

## State of the Art

| Ancienne façon (avant v1.5) | Façon actuelle | Depuis | Ce que ça implique pour la phase 20 |
|---|---|---|---|
| `SourceReliability.Estimated` = estimation absolue par comptage de tokens | `Estimated` = **plancher** (borne inférieure), produit par une seule branche | Phase 19 | `IsEstimated` est un **nom périmé** ; les 4 textures de cadran marquent déjà le bon concept |
| Staleness au niveau **snapshot** (`SourceCapturedAt`) | Fraîcheur **par fenêtre** (`WindowState.CapturedAt` + `Provenance`) | Phase 19 | `MainViewModel.IsStale` est un vestige d'une architecture révolue |
| « 10 % » affiché indéfiniment | Limite d'âge de **6 min**, réhabilitation par preuve d'inactivité, sinon indisponible | Phase 19 | Les quatre apparences existent en DONNÉES ; il ne manque que leur rendu |
| `UtilizationToBrushConverter` (rampe statique) | `ChronosTheme.ArcBrush` (rampe **du thème actif**, 9 thèmes) | Refonte visuelle | Ne pas réintroduire de converter de couleur ; le converter subsiste pour la compatibilité et a ses 7 tests |
| 1 style de cadran | **5 styles**, 2 modes, 9 thèmes | Refonte visuelle | Toute marque doit être posée **une fois**, dans la couche racine |

**Périmé / à ne pas réactiver :**
- `WindowState.EstimatedTokens` — mort en production, garde de non-retour en place.
- `PercentFormatter.Format(double?, bool)` (préfixe « ~ ») — **conservée volontairement** pour
  `CadranPreviewViewModel` (galerie `--cadrans`), qui n'a aucune provenance à exhiber. Ne pas la supprimer.
- Le « ~ » de `DiagnosticService.Describe` — lui, est un **oubli**, pas une conservation volontaire.

---

## Runtime State Inventory

*Phase de dessin et de branchement — pas de renommage de chaîne traversant l'infrastructure. Les cinq
catégories sont néanmoins renseignées explicitement, parce que le renommage `IsEstimated` → `EstPlancher`
est un renommage réel.*

| Catégorie | Trouvé | Action requise |
|---|---|---|
| **Données stockées** | `%APPDATA%\Chronos\last-exact.json` (ABSENT sur cette machine), `usage.json` (77 o, mtime 1783666519). **Aucune** ne persiste `IsEstimated`, `Provenance` ni `Source` : `LastExactStore.Payload/Entry` ne sérialise que `utilization` / `resets_at` / `captured_at`. `SourceUsage` n'a donc **aucun impact de format de fichier** — et c'est correct : le magasin ne contient par construction que de l'exact | **Aucune** — vérifié par lecture de `LastExactStore` |
| **Config de service vivante** | `%APPDATA%\Chronos\settings.json` — lu : ne contient **aucune** clé liée à `IsEstimated`/`Provenance`. `CadranStyle: "Arcs"`, `CadranMode: "Normal"`, `ThemeKey: "ardoise"` : ces trois clés **influencent les tests** (Piège 2) mais aucune migration n'est requise | **Aucune migration.** Mais le plan doit neutraliser leur influence sur les tests |
| **État enregistré par l'OS** | `~/.claude/settings.json` — 25 hooks Chronos + `statusLine`. **Non touché par cette phase**, et **interdit de le déclencher** (lancer l'overlay purge les hooks) | **Aucune** — et ne pas lancer l'overlay |
| **Secrets / variables d'env.** | `%APPDATA%\Chronos\oauth.dat` — **518 octets, mtime 1783863147**. Aucun nom de clé renommé par cette phase | **Aucune.** Relever `stat` avant/après le travail, comme en 19-05 |
| **Artefacts de build / paquets installés** | `src/Chronos/bin`, `tests/Chronos.Tests/bin`, `TestResults/`. L'**exe publié** lancé par l'autostart porte encore le comportement d'avant la phase 19 (constat 19-05) | **Aucune pour la phase.** La republication reste une décision de release, hors périmètre — mais le rappeler à l'utilisateur : tant qu'il ne republie pas, il ne verra **rien** de cette phase |

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build + test | ✓ | compile `net8.0-windows`, build test 5,2 s | — |
| xunit + xunit.runner.visualstudio | suite | ✓ | 2.9.2 / 2.8.2 | — |
| **Xunit.StaFact (`[WpfFact]`)** | tout test BAML d'EXA-03 | ✓ | 1.1.11 | — |
| Session Windows interactive (STA) | `[WpfFact]` construisant `MainWindow` | ✓ | 692+7 tests verts, y compris 14 `[WpfFact]` | — |
| `%APPDATA%\Chronos\` | lecture par `CadranBindingTests` | ✓ | `settings.json` présent, `oauth.dat` 518 o, `usage.json` 77 o, `last-exact.json` **ABSENT** | — |
| Réseau vers `api.anthropic.com` | **AUCUNE tâche de cette phase** | — | — | **interdit** en test (contrainte de sécurité) |
| Application de bureau Claude (UIA) | `DiagnosticService` section bureau | ✗ (0 session détectée, santé UIA `Ok`) | — | ✅ le poll rend une liste vide sans erreur — **et la couture Q5 le rend inutile en test** |
| Skills `frontend-design` / `windows-wpf` | tâches de dessin (CLAUDE.md) | ⚠️ **aucun `.claude/skills/` ni `.agents/skills/` dans ce dépôt** | — | skills globaux : à charger par nom, pas depuis le dépôt |

**Dépendances manquantes sans repli :** aucune.
**Dépendances manquantes avec repli :** l'app de bureau Claude (poll UIA) — la couture de la Question 5 la
retire du chemin de test ; en production, son absence est déjà gérée (message explicite).

---

## Validation Architecture

### Test Framework

| Propriété | Valeur |
|---|---|
| Framework | **xUnit 2.9.2** + **Xunit.StaFact 1.1.11** (`[WpfFact]`, thread STA) |
| Fichier de config | `tests/Chronos.Tests/Chronos.Tests.csproj` (pas de `xunit.runner.json`) ; collection sérialisée définie dans `XamlWpfCollection.cs` |
| Commande rapide | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --no-build -c Debug --filter "FullyQualifiedName~<Classe>"` |
| Suite complète | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug` |
| Baseline mesurée | 699 verts · **2 s** hors `DiagnosticServiceTests` · **2 min 10 s** avec |

### Phase Requirements → Test Map

| Req | Comportement | Type | Commande automatisée | Fichier existe ? |
|---|---|---|---|---|
| EXA-03 | Le plancher rend l'arc des **Anneaux** en pointillé (5ᵉ style enfin marqué) | BAML `[WpfFact]` | `--filter "FullyQualifiedName~CadranBindingTests"` | ✅ (à étendre) |
| EXA-03 | Le plancher rend `EstPlancher=true` et les 4 DP `Grain` des styles alternatifs | unité `[Fact]` | `--filter "FullyQualifiedName~WindowGaugeViewModelTests"` | ✅ (à étendre) |
| EXA-03 | « exact daté » allume la marque d'âge, « exact frais » ne l'allume PAS | unité + BAML | `…~MainViewModelTests` / `…~CadranBindingTests` | ✅ |
| EXA-03 | Un `EncoreValide` ne porte **ni** « ≥ » **ni** texture (non-régression d'honnêteté, Piège 5) | unité `[Fact]` | `…~WindowGaugeViewModelTests` | ✅ |
| EXA-03 | `DataUnavailable` allume le mot « indisponible », résolu sur la `Visibility` réelle | BAML `[WpfFact]` | `…~CadranBindingTests` | ✅ |
| EXA-03 | Les **4** pastilles ne se recouvrent jamais et restent à > 71,5 px du centre ; empreinte 170×170 intacte | BAML `[WpfFact]` | `…~CadranBindingTests` | ✅ (motif `MonterPastille`) |
| EXA-06 | Le rapport nomme **chacune des 5 sources** par son libellé FR | unité `[Fact]` | `…~DiagnosticServiceTests` | ✅ |
| EXA-06 | Le rapport donne l'**ancienneté** du relevé de chaque fenêtre | unité `[Fact]` | `…~DiagnosticServiceTests` | ✅ |
| EXA-06 | `Source = null` rend « non renseignée », **jamais** un nom de source par défaut | unité `[Fact]` | `…~DiagnosticServiceTests` | ✅ |
| EXA-06 | `Source` **survit** aux trois composites imbriqués (preuve par référence, motif `Le_repli_le_plus_interne_…`) | unité `[Fact]` | `…~CompositeUsageProviderTests` ou `…~GardesDoctrineTests` | ✅ |
| Garde | Aucun seuil d'ancienneté calculé dans `ViewModels/` (remplaçant d'`IsStale`) | garde source | `…~GardesDoctrineTests` | ✅ |
| Garde | Aucun champ `EstimatedTokens` dans `Models/` (remplaçant du test comportemental) | garde source | `…~GardesDoctrineTests` | ✅ |
| Perf | `DiagnosticServiceTests` s'exécute en **< 5 s** pour ses 7 tests | mesure | `time dotnet test … --filter "…~DiagnosticServiceTests"` | ✅ |
| Manuel | Contraste et lisibilité des 4 marques sur fond d'écran réel, 5 styles × 2 modes | **manuel** | — (voir plus bas) | — |

### Sampling Rate

- **Par commit de tâche :** `dotnet test --no-build --filter "FullyQualifiedName~<ClasseTouchée>"` — **< 3 s**
  pour toute classe autre que `DiagnosticServiceTests`.
- **Par fusion de vague :** suite complète.
- **Porte de phase :** suite complète **DEUX FOIS** de suite (protocole 19-05 : la phase ajoute des
  `[WpfFact]`, le chargeur BAML a un précédent de course) + les 5 gardes permanentes + relevé `stat` de
  `oauth.dat` avant/après (attendu : `518 1783863147`).

### Wave 0 Gaps

- [ ] `tests/Chronos.Tests/Fakes/FakeInventaireMachine.cs` — faux à listes vides pour `IInventaireMachine`
      (couvre la dette de durée et débloque toute itération rapide sur `DiagnosticServiceTests`).
- [ ] Étendre `GardesDoctrineTests` — 2 gardes de source (seuil d'ancienneté dans `ViewModels/`,
      absence d'`EstimatedTokens`). Le motif `CheminSources()` est déjà écrit, à réutiliser tel quel.
- [ ] **Décider en vague 0** si `CadranBindingTests.BuildWindow` migre vers `TempPaths()` (Piège 2). C'est un
      choix structurant : il conditionne la forme de **tous** les tests de style de la phase.
- [ ] Aucune installation de framework nécessaire.

### Vérifications manuelles (ce qu'aucun test ne peut produire)

Cette phase est visuelle : une part irréductible de sa preuve est humaine. À **nommer explicitement** dans le
plan plutôt qu'à simuler (précédent 19-05, où la substitution a été documentée comme telle).

- Lisibilité des 4 marques sur **fond d'écran réel**, dans les 5 styles × 2 modes (≈ 10 captures).
- Que le pointillé des Anneaux se distingue du plein **à distance de lecture normale**, pas au zoom.
- Que la rangée de 4 pastilles n'empiète sur aucun anneau, dans les 9 thèmes.
- ⚠️ **Rappel de sécurité :** ces vérifications exigent de lancer l'overlay, ce qui **purge les 25 hooks de
  `~/.claude/settings.json`**. Elles ne peuvent être faites que **par l'utilisateur, sous supervision**, avec
  une sauvegarde préalable. Le plan doit les délivrer comme un protocole, pas les exécuter.

---

## Open Questions

1. **M-âge : portée globale ou par fenêtre ?**
   - Ce qu'on sait : `Provenance` est par fenêtre ; un test existant impose la portée par fenêtre **pour la
     marque du pourcentage** ; la couche racine est le seul endroit style-agnostique, et elle est globale.
   - Ce qui reste flou : existe-t-il un scénario réel où les deux fenêtres divergent en âge ? La recherche
     n'en a trouvé aucun (elles viennent de la même chaîne), mais ne peut pas le prouver.
   - Recommandation : **globale, en affichant le plus vieux des deux**, avec le détail par fenêtre dans
     l'infobulle. Écrire la règle « jamais le plus jeune » dans le code et la verrouiller par un test.

2. **La forme exacte du glyphe de M-âge.**
   - Ce qu'on sait : le canal (position, couche racine), le token (`TexteSecondaire`), la taille (14 px max),
     la place (rangée bas-droite, x ≥ 96), et qu'elle doit être **informative et additive**, jamais
     soustractive.
   - Ce qui reste flou : anneau creux, point, tiret, sablier ?
   - Recommandation : laisser au planner **avec la skill `frontend-design`** (CLAUDE.md l'exige sur les
     tâches UI). La recherche a fixé toutes les contraintes ; le choix esthétique n'en est plus un risque.

3. **`CadranBindingTests` : migrer vers `TempPaths()` ou neutraliser localement ?**
   - Ce qu'on sait : 14 tests dépendent aujourd'hui du `settings.json` réel (Piège 2).
   - Ce qui reste flou : la migration casse-t-elle un test qui dépendrait implicitement de l'état persisté ?
     Non mesuré (la migration n'a pas été tentée — elle modifierait le dépôt).
   - Recommandation : trancher en **vague 0**, pas en cours de route.

4. **Renommer les DP `Estimated` des 3 contrôles en `Grain` ?**
   - Ce qu'on sait : le coût est faible (3 DP + 6 bindings) et le découplage est propre.
   - Ce qui reste flou : rapport bénéfice/risque en fin de milestone.
   - Recommandation : **optionnel**, en dernière tâche. À sacrifier en premier si la phase déborde.

---

## Sources

### Primaires (confiance HIGH — code du dépôt, lu intégralement)

| Fichier | Ce qui en a été tiré |
|---|---|
| `src/Chronos/Models/{WindowState,UsageSnapshot,ProvenanceReleve,SourceReliability}.cs` | `required` × 2 uniquement ⇒ 0 site cassé ; les 4 situations ; le legacy `SourceCapturedAt` |
| `src/Chronos/Services/DoctrineFraicheur.cs` | `LimiteAge` = 300 s + 60 s = **6 min** ; `plancher ⊂ daté` (ordre des branches) ; `Qualifier`/`Indisponible` |
| `src/Chronos/Services/{Composite,LastExact}UsageProvider.cs` | `Best()` rend l'instance **par référence** ; `with` au-dessus vs `new` dans le composite |
| `src/Chronos/Services/DiagnosticService.cs` (600 l.) | `Describe()` ; le « ~ » incohérent ; `FindTokenVaults` ; les 2 précédents de paramètre optionnel terminal |
| `src/Chronos/Controls/Cadrans/{EmberRingControl,FuseBar,TideColumn,FlapRow}.cs` | Inventaire du canal texture : **4 styles sur 5 l'ont déjà** |
| `src/Chronos/Views/Cadrans/*.xaml`, `MainWindow.xaml` | Couleurs en **dur** dans les 4 `UserControl` ; la couche racine et son commentaire ; géométrie des pastilles |
| `src/Chronos/Views/SettingsWindow.xaml` | `UniformGrid Columns="2"` + 3 boutons ; panneau 330 px, `Padding 16`, style `Flat` `Padding="10,7"` ; **3 marges fausses** |
| `src/Chronos/Theming/ChronosTheme.cs` | **9 thèmes** (pas 3) ; `BrushTokens()` écrase `TexteSecondaire` et `Alerte` par thème |
| `src/Chronos/ViewModels/{MainViewModel,WindowGaugeViewModel,CadranPreviewViewModel}.cs` | `IsStale` l. 396 ; `MajPastilles` (la collision hors-ligne/invitation) ; `IsEstimated` |
| `src/Chronos/App.xaml.cs` l. 347-357 | La chaîne DI réelle ⇒ les **5** sources nommables |
| `tests/Chronos.Tests/GardesDoctrineTests.cs` | La garde porte sur **`UsageSnapshot`**, pas `WindowState` |
| `tests/Chronos.Tests/XamlWpfCollection.cs` | Le « pourquoi » de `DisableParallelization` (course `WpfXamlType.FindKnownMember`) |
| `tests/Chronos.Tests/{CadranBinding,ReglagesBinding,WindowGaugeViewModel,MainViewModel,DiagnosticService}Tests.cs` | Les 13 tests impactés, nominativement ; `MonterPastille` ; `ChronosPaths.Default()` vs `TempPaths()` |
| `.planning/phases/19-.../19-05-SUMMARY.md` | Les 11 dettes, la réserve sur DEL-04, le protocole de porte de phase |

### Mesures de première main (confiance HIGH — exécutées sur cette machine, 2026-09-12)

| Mesure | Méthode | Résultat |
|---|---|---|
| Durée de la suite hors diagnostic | `dotnet test --no-build -c Debug --filter "FullyQualifiedName!~DiagnosticServiceTests"` | **692 tests / 2 s** |
| Durée de `DiagnosticServiceTests` | `dotnet test --no-build -c Debug --filter "…~DiagnosticServiceTests"` | **7 tests / 2 min 8 s** |
| Décomposition d'un `BuildReportAsync` | Projet console **hors dépôt** (scratchpad), réplique exacte de `FindTokenVaults` + appels publics | `FindTokenVaults` **17 703 ms**, UIA **936 ms**, reste **< 50 ms** |
| Largeur des libellés des réglages | `System.Windows.Media.FormattedText`, Segoe UI, 12, fr-FR, 96 dpi | « Recalibrer hebdo » **98,5 px** (+ « … » ≈ 9) ; « Source terminal » **83,0 px** |
| État réel de la machine | `cat` / `stat` en **lecture seule** | `settings.json` : `Arcs` / `Normal` / `ardoise` ; `oauth.dat` 518 o mtime 1783863147 ; `last-exact.json` **ABSENT** ; `~/.claude/projects` **1,2 Go**, 839 `.jsonl` |

> **Sécurité de la campagne de mesure :** aucune requête réseau, aucun jeton déchiffré, aucune écriture dans
> `%APPDATA%\Chronos\`, `~/.claude/settings.json` non touché, **overlay jamais lancé**. Le projet de sonde vit
> dans le scratchpad de session et n'a pas été ajouté au dépôt ni à la solution.

### Secondaires (confiance MEDIUM)

- Loi des variables visuelles (Bertin) invoquée pour justifier l'orthogonalité **texture ⊥ luminance ⊥
  longueur** : principe de sémiologie graphique, pas un fait vérifiable dans ce dépôt. Le fait **vérifié** est
  que les quatre contrôles alternatifs utilisent effectivement grain et pointillé pour marquer, sans toucher
  ni à la longueur ni à la couleur de quota.
- Le rendu de `StrokeDashArray` sur `RingArc` (`Shape`) n'a **pas été exécuté** — la contrainte de ne pas
  lancer l'overlay l'interdisait. L'héritage de la propriété est certain (`RingArc : Shape`) ; le résultat
  visuel exact au rayon 38/52/54 reste à constater. **C'est le premier point à vérifier à l'exécution.**

---

## Metadata

**Répartition de la confiance :**

| Domaine | Niveau | Raison |
|---|---|---|
| Stack (rien à ajouter) | **HIGH** | Tout est dans le framework ; `.csproj` lus |
| Inventaire des canaux visuels par style | **HIGH** | Les 5 vues et les 4 contrôles lus intégralement |
| Question 4 (0 site cassé, 0 garde cassée) | **HIGH** | `required` × 2 comptés ; la garde lue ligne à ligne |
| Question 5 (dette de durée) | **HIGH** | Mesuré trois fois, décomposé, coupable isolé à 94 % |
| Question 6 (`UniformGrid`) | **HIGH** | Largeurs mesurées par `FormattedText`, arithmétique de cellule explicite |
| Question 8 (tests impactés) | **HIGH** | 13 tests localisés à la ligne par `grep` sur les identifiants concernés |
| Question 1 (sémiologie, 3 marques / 4 apparences) | **MEDIUM-HIGH** | L'argument `plancher ⊂ daté` est **prouvé par le code** ; le choix de ne rien marquer sur « frais » est un jugement de design défendable, pas un fait |
| Question 2 (forme exacte de M-âge) | **MEDIUM** | Le canal et les contraintes sont établis ; le glyphe reste ouvert (Open Question 2) |
| Rendu réel du pointillé sur l'arc | **MEDIUM** | Non exécuté (interdiction de lancer l'overlay) |

**Date de recherche :** 2026-09-12
**Valable jusqu'au :** 2026-10-12 (30 j). Les mesures de durée et les largeurs de texte sont attachées à
**cette machine** ; les faits de code sont attachés au commit `71413c1`.
