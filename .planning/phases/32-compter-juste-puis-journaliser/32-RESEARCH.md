# Phase 32 : Compter juste, puis journaliser — Research

**Researched:** 2026-09-27 (01:30 → 02:10, heure locale)
**Domain:** persistance locale JSONL (.NET 8 / WPF, BCL seule), dédup de transcripts, mono-instance Windows, virtualisation AppData MSIX
**Confidence:** HIGH sur les faits mesurés (sonde hors arbre, scans de transcripts, essais .NET 8) ; MEDIUM sur les choix de forme laissés à discrétion

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Décisions de l'utilisateur (verrouillées)
- La phase se publie seule en **3.2.2** avant toute UI : chaque jour sans journal de pourcentages est perdu à jamais.
- Toutes les variantes de la fenêtre (A/B/C) seront codées plus tard (phase 34) : cette phase ne fait AUCUNE UI, mais la
  lecture par plage (JRN-05) doit servir les trois vues (Semaine de forfait, Jour, 4 semaines) sans retouche.
- Le constat reporté de v1.7 (une seule instance, tableau des gestes, 12 vérifications) est le dernier plan de la phase, joué
  avec l'utilisateur sur la 3.2.2 ; l'agent ne lance, n'arrête ni ne clique jamais l'overlay.

#### Approche technique arrêtée par le conseil (`.zeus/reports/llm-council-2026-09-26.md`)
- Deux journaux de nature différente, jamais fusionnés ; cette phase ne livre que celui des RELEVÉS EXACTS.
- Format : JSONL mensuel tolérant (`%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl`), dialecte maison de `LastExactStore`
  (SchemaVersion, ligne invalide ignorée, `v` inconnu sauté) ; **pas de SQLite** (dépendance native), pas de CSV.
- Ligne de relevé `{v, t, u5, r5, u7, r7, statut5, statut7, overage, source}` ; lignes d'événement
  `{v, t, ev: demarrage|arret|jeton_invalide|sonde_refusee|reprise}`.
- Dédup : `CapturedAt` strictement croissant (l'orchestrateur ressert le même relevé 5 fois sur 6 : sonde 300 s, timer 60 s) ;
  clé d'idempotence `CapturedAt` + source pour survivre à plusieurs écrivains ; append atomique (une ligne < 4 Ko).
- Point d'accroche : décorateur/abonné posé au même niveau que `LastExactUsageProvider` (ou sur `RefreshOrchestrator.SnapshotChanged`),
  n'écrivant que des fenêtres `Reliability == Exact` produites par l'inner, jamais un plancher ni une valeur rejouée du magasin.
- L'âge de la dernière écriture de chaque magasin persistant est un chiffre de première classe (diagnostic + réglages) ;
  alerte au-delà de 15 min alors que Chronos tourne (« journal muet depuis N min », pastille `Alerte`).
- Mono-instance : mutex nommé ; le second exe se retire en le disant (message) sans jamais tuer l'autre ; `--hook` et le mode
  CLI restent multi-instances.
- Lecture par plage en classes NEUTRES (`Services/` ou `Rendering/` sans WPF, `ServicesLayerPurityTests` reste vert) : série,
  trous (> 2 cadences = 10 min) avec leur cause, resets 5 h et hebdo observés, Δ entre relevés consécutifs de même `resets_at`
  (jamais à travers un reset), saut non localisé de part et d'autre d'un trou. Bornes hebdo : `resets_at` 7 j du journal, repli
  `WeeklyAnchor` ; semaine de forfait = samedi 00:00 heure locale (2026-09-18T22:00Z constaté).
- Hypothèses à écrire dans `docs/data-sources.md` § « Journal d'historique », à vérifier AVEC le journal : granularité des
  en-têtes `utilization` (0,01 constaté) ; Δ d'utilization = consommation (pas de recalcul rétroactif hors reset) ; reset hebdo à
  l'heure locale au changement d'heure du 25/10/2026 (`WeeklyWindow` avance par 7 × 24 h fixes et dériverait d'une heure).

### Claude's Discretion
Nommage des classes, découpage des fichiers, forme exacte du mutex, stratégie de test des écrivains concurrents, format des
lignes de diagnostic — dans le respect des conventions du dépôt (MVVM, DI, français, commentaires qui disent POURQUOI).

### Deferred Ideas (OUT OF SCOPE)
- Agrégats de tokens, curseurs, reconstruction de fond → phase 33 (TOK).
- Fenêtre Historique, styles A/B/C, vues Jour et 4 semaines → phases 34-35 (HIS, ACC).
- Compaction du journal, heatmap, export CSV, projection conditionnelle → v1.9 (v2 requirements).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description (résumé) | Research Support |
|----|----------------------|------------------|
| CPT-01 | Dédup par `message.id` (repli `requestId`) avant toute somme de `usage` ; fixture réelle multi-blocs ; `TokensDepuisReleve` en hérite ; garde « aucun lecteur sans dédup » | §CPT-01 : un seul lecteur en production (`TranscriptActivityProvider.SumUsageTokens`, l. 136-144) ; **règle mesurée : max par champ (= dernière ligne) par id, PAS la première ligne** (streaming `output_tokens` depuis Claude Code 2.1.260) ; 491 ids présents dans plusieurs fichiers ; gabarit de fixture anonymisé ; garde textuelle sur `"usage"` |
| CPT-02 | Cause du gel de `last-exact.json` établie/corrigée/testée ; âge de dernière écriture des trois magasins au diagnostic ; écriture ratée non silencieuse | §CPT-02 : **le gel n'existe pas dans la vue réelle** (sonde WMI 01:56 : fichier réécrit à 01:55:37) — c'est la copie COW du paquet MSIX que toute session lit ; correctif = observabilité (`DerniereEcriture`, `DerniereErreur`, ligne « Vue AppData » au diagnostic), test du try/catch muet |
| CPT-03 | Mono-instance par mutex nommé, message, « N processus Chronos » au diagnostic, `--hook`/CLI exemptés | §CPT-03 : `Mutex(true, @"Local\Chronos-overlay", out createdNew)` après les court-circuits CLI (App.xaml.cs l. 20-57) et avant `Host.CreateApplicationBuilder()` ; comptage par `Process.GetProcesses()` + préfixe (vérifié sans admin ; les hooks apparaissent de façon éphémère) |
| JRN-01 | Chaque relevé exact distinct → `releves-AAAA-MM.jsonl` ; jamais un rejeu du cache, un plancher ni une valeur du magasin | §JRN-01 : décorateur ENTRE `LastExactUsageProvider` et le composite (voit l'inner brut) ; dédup `t` strictement croissant par `source` |
| JRN-02 | Événements `demarrage`, `arret`, `jeton_invalide`, `sonde_refusee`, `reprise` | §JRN-02 : `IHostedService` (Start/Stop, ordre d'inscription), `IAuthStatus.EtatChange` → `Deconnecte`, `IEtatServeur.DernierResultat` sur transition, `reprise` calculée par l'écrivain |
| JRN-03 | Append atomique idempotent (clé `CapturedAt`+source), lecture tolérante, mensuel, 24 mois, types neutres | §JRN-03 : **`FileMode.Append` n'est PAS atomique sous Windows (mesuré : deux appenders → une ligne perdue)** → `FileShare.None` + reprise bornée + relecture de la queue sous le verrou ; piège du namespace dans `ServicesLayerPurityTests` |
| JRN-04 | Âge de la dernière écriture au diagnostic et aux réglages ; alerte > 15 min si Chronos tourne | §JRN-04 : `MainViewModel.MajTexteEtatSonde` (l. 403) et `LigneEtatSonde` (SettingsWindow.xaml l. 195) sont le modèle ; règle « 15 min depuis max(démarrage, dernière écriture) » ; `LibelleSource.Anciennete` pour les mots |
| JRN-05 | Lecture par plage en classes pures : série, trous, resets observés, Δ de même `resets_at`, saut non localisé | §JRN-05 : types proposés, bornes samedi 00:00 local calculées avec `TimeZoneInfo` (vérifié : 2026-10-24 → 22:00Z, 2026-10-31 → 23:00Z, semaine de 169 h ; dérive de 1 h de `NextReset` confirmée) |
| JRN-06 | Release 3.2.2 sans UI, quatre propriétés du csproj, `docs/data-sources.md` § « Journal d'historique » | §JRN-06 : procédure 31-02 recopiée pas à pas, garde documentaire à l'image de `ContratHooksDocumenteTests` |
| VAL-04 | Constat 31-CONSTAT rejoué sur la 3.2.2 + « le journal s'écrit » | §VAL-04 : sonde WMI (script reproduit ici), trois overlays encore en marche (PID 40772, 126160, 121900), sortie sous `Documents\chronos-constat` |
</phase_requirements>

## Summary

Trois faits mesurés pendant cette recherche changent la façon de planifier la phase.

**1. Le « gel de `last-exact.json` » n'existe pas — c'est un artefact de la virtualisation MSIX.** Une sonde lancée par WMI hors
de l'arbre de l'app bureau (parent `WmiPrvSE`, `%APPDATA%\Claude` absent = vue réelle) à 01:56:15 a lu
`%APPDATA%\Chronos\last-exact.json` écrit à **01:55:37**, contenu `captured_at = 2026-09-26T23:53:37Z` (u5 0,01 / r5 04:50Z ;
u7 0,41 / r7 2026-10-02T22:00Z) — c'est le cache de la sonde (300 s) rejoué chaque minute par `Save`. La sonde de phase 29
(25/09 21:17) montrait déjà `last-exact.json 25.09 21:16`. Ce que toute session Claude Code lit (y compris mon Bash) est la copie
copy-on-write du paquet `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Chronos\last-exact.json`, figée au
2026-09-13 12:44:59 : ce jour-là un overlay a tourné SOUS l'arbre de l'app de 07:36:58 (COW de `settings.json` et
`archived.json` = purge au démarrage) à 12:44:59 (dernier `Save`), puis plus jamais. Le `try/catch` muet de
`LastExactUsageProvider` n'a rien avalé ; il reste un défaut d'observabilité à corriger (CPT-02), et le diagnostic doit désormais
dire dans quelle **vue** il écrit.

**2. La règle de dédup n'est pas « une ligne recopie le même usage ».** Sur 934 fichiers des 8 derniers jours (1,28 Go, 128 545 lignes
`assistant`, 60 349 `message.id`, 0 ligne sans id) : depuis Claude Code **2.1.260** (2.1.260 → 2.1.281), les lignes d'un même id
portent un `output_tokens` **partiel et croissant** (streaming : 8 → 8 → 256), les trois autres champs identiques ; la **dernière
ligne = le max par champ** (12 835 / 12 835 cas). Sommer toutes les lignes surcompte ×2,1 ; prendre la première ligne sous-compte la
sortie. De plus **491 ids apparaissent dans plusieurs fichiers** (reprise/fork de session) : la dédup doit être **globale à la passe**
pour la question « tokens depuis T » (compte facturé une fois), le périmètre « par fichier » restant celui des curseurs de la phase 33.

**3. `FileMode.Append` n'est pas un append atomique sous Windows.** Essai .NET 8.0.25 : deux `FileStream(Append, FileShare.ReadWrite)`
ouverts sur le même fichier, chacun écrit une ligne → le fichier ne contient que la seconde. L'idempotence multi-écrivains exige un
verrou exclusif (`FileShare.None` + reprises bornées, motif déjà éprouvé par `EcritureEtatSession`) sous lequel on relit la queue du
fichier avant d'écrire.

**Primary recommendation :** écrire le journal en décorateur `IUsageProvider` placé ENTRE `LastExactUsageProvider` et le composite
(il ne voit que l'inner brut : jamais le magasin, jamais un plancher), avec un écrivain `FileShare.None` + relecture de queue, un
helper de dédup « max par `message.id` » unique pour tous les lecteurs de `usage`, un mutex `Local\Chronos-overlay` posé avant le
Host, et deux lignes de diagnostic nouvelles : « Vue AppData : réelle | virtualisée (paquet) » et « dernière écriture » par magasin.

## Project Constraints (from CLAUDE.md)

- Stack imposée : C# / .NET 8 / WPF / MVVM strict (CommunityToolkit.Mvvm 8.4.2, `[ObservableProperty]`/`[RelayCommand]`) /
  `Microsoft.Extensions.Hosting` 8.0.1. **Aucune dépendance NuGet nouvelle** (surtout pas SQLite ni `System.Management`).
- Dossiers `Models/Views/ViewModels/Services` ; types neutres (aucun WPF) sous `Services/` et `Models/` ; `IClock` injecté ;
  chemins uniquement via `ChronosPaths` (jamais `Assembly.Location`, vide en mono-fichier ; jamais de chemin en dur).
- Chemins sous le profil utilisateur, aucun droit admin ; lecture seule stricte de `~/.claude` et `%APPDATA%\Claude` ; aucun appel
  réseau supplémentaire (le journal ne sonde rien : il observe ce que la chaîne produit).
- Honnêteté des chiffres : `utilization`/`resets_at` prioritaires ; jamais une estimation présentée comme exacte ; aucune source ≠
  crash ; parsing tolérant.
- UI et commentaires en **français**, commentaires qui disent POURQUOI.
- Publication : exe self-contained mono-fichier win-x64, `PublishTrimmed=false`, version aux quatre propriétés du csproj et dans le
  nom du fichier ; autostart `shell:startup`.
- GSD : toute édition passe par `/gsd:execute-phase` ; l'agent ne lance jamais l'overlay.

## Standard Stack

Aucun paquet nouveau. Tout est dans la BCL de net8.0-windows.

### Core
| API | Version | Purpose | Why Standard |
|-----|---------|---------|--------------|
| `System.Text.Json` (`JsonSerializer`, `JsonDocument`, `JsonStringEnumConverter`) | intégré net8.0 | Écriture d'une ligne JSONL, lecture tolérante ligne par ligne | Déjà le dialecte de `LastExactStore` et de `TranscriptActivityProvider` (`JsonDocument.Parse(line)` + `catch (JsonException)`) |
| `FileStream(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)` | intégré | Append exclusif + relecture de queue sous verrou | Seule forme atomique inter-processus mesurée ; `File.Move` temp→cible reste le motif des magasins à réécriture complète |
| `System.Threading.Mutex(bool, string, out bool)` | intégré | Verrou mono-instance `Local\Chronos-overlay` | Motif Windows canonique ; docs : « le thread appelant possède le mutex nommé seulement si `createdNew` est vrai ; sinon `WaitOne` » |
| `System.Diagnostics.Process.GetProcesses()` | intégré | « N processus Chronos » | Vérifié sans admin : `Chronos-v3.1.0#40772, Chronos-v3.2.0#126160, Chronos-v3.2.1#121900` (+ 2 hooks éphémères au moment de l'essai) |
| `TimeZoneInfo` (`Local`, `FindSystemTimeZoneById`, `GetUtcOffset`) | intégré | Bornes samedi 00:00 heure locale, DST | Vérifié sur cette machine : `Local.Id = "Romance Standard Time"`, `"Europe/Paris"` accepté (`HasIanaId = True`) |
| `Microsoft.Extensions.Hosting` `IHostedService` | 8.0.1 (déjà référencé) | `demarrage`/`arret`, ordre Start/Stop | Motif de `RefreshOrchestrator` et `TokenRefreshService` (instance unique réexposée par `AddHostedService(sp => …)`) |

### Supporting (déjà dans le dépôt, à réutiliser)
| Type | Fichier | Rôle dans la phase |
|------|---------|--------------------|
| `LastExactStore` | `Services/LastExactStore.cs` | Modèle du dialecte (SchemaVersion, options JSON, temp + `File.Move`) ; reçoit `DerniereEcriture`/`DerniereErreur` |
| `LastExactUsageProvider` | `Services/LastExactUsageProvider.cs` | Tête de chaîne ; son inner devient le décorateur de journalisation |
| `EcritureEtatSession.EcrireAvecReprise` | `Services/EcritureEtatSession.cs` (l. 114 +) | Motif « écrivains concurrents » : partage, `EssaisMax = 60`, relecture avant écriture |
| `BalayageMagasinSessions` | `Services/BalayageMagasinSessions.cs` | Motif de purge par âge (rétention 24 mois : lister, parser le nom, supprimer, compter les échecs) |
| `DoctrineFraicheur.LimiteAge` | `Services/DoctrineFraicheur.cs` l. 36 | Précédent « constante DÉRIVÉE de `CadenceNominale` » → seuil de trou `2 × CadenceNominale` |
| `LibelleSource.Anciennete` | `Text/LibelleSource.cs` l. 47 | Mots de l'âge (« à l'instant », « il y a N min », « il y a N h MM », « il y a N j ») |
| `RacinesEtat.Candidats` | `Services/RacinesEtat.cs` l. 48 | Détection du paquet `Packages\Claude_*\LocalCache\Roaming` → ligne « Vue AppData » du diagnostic |
| `FakeClock`, `FakeUsageProvider`, `FakeAuthStatus`, `FakeEtatServeur` | `tests/Chronos.Tests/Fakes/` | Tous les faux nécessaires existent déjà |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `FileShare.None` + reprises | Mutex nommé `Local\Chronos-journal` autour d'un `FileMode.Append` | Fonctionne aussi, mais ajoute un second objet noyau et ne protège pas d'un écrivain qui ignore le mutex ; le verrou de fichier est la vérité du système |
| Décorateur entre tête et composite | Abonnement à `RefreshOrchestrator.SnapshotChanged` | L'événement porte le snapshot APRÈS doctrine : il faudrait filtrer `Source != MagasinDernierExact` et `Provenance == Frais`, et le motif « abonné résolu avant le Host » a déjà mordu (phase 17) — le décorateur voit l'inner brut sans filtre fragile |
| Dossier `Services/Historique/` + sous-namespace | Fichiers à plat `Services/Journal*.cs` | Le sous-namespace ÉCHAPPE à `ServicesLayerPurityTests` (test d'égalité stricte du namespace) et aux gardes textuelles « dossiers plats, non récursif » : si sous-dossier, élargir ces gardes dans le MÊME plan |
| Nom de fichier par mois UTC | mois local | UTC = déterministe (pas d'ambiguïté DST), cohérent avec `t` en UTC et les tranches UTC de TOK-01 ; le lecteur ouvre les mois qui chevauchent la plage convertie en UTC |

**Installation :** rien à installer. Vérification : `dotnet --list-sdks` → `10.0.201` ; runtime d'exécution des tests `8.0.25`.

## Architecture Patterns

### Carte des fichiers proposée (discrétion de Claude, cohérente avec le dépôt)

```
src/Chronos/
├── Models/
│   └── Historique/                       # ou à plat : ReleveJournal.cs, EvenementJournal.cs
│       ├── ReleveJournal.cs              # record {T, U5, R5, U7, R7, Statut5, Statut7, Overage, Source}
│       ├── EvenementJournal.cs           # record {T, Ev, Cause?} ; enum TypeEvenement
│       └── LectureJournal.cs             # résultat de JRN-05 : Serie, Trous, Resets, Deltas, Sauts
├── Services/
│   ├── ChronosPaths.cs                   # + HistoriqueDir (propriété calculée, comme LastExactFile)
│   ├── LastExactStore.cs                 # + DerniereEcriture / DerniereErreur (plus de silence)
│   ├── VerrouInstanceUnique.cs           # mutex nommé, neutre, testable
│   ├── InventaireProcessus.cs            # « N processus Chronos » (Process.GetProcesses + préfixe + âge)
│   └── Historique/
│       ├── JournalReleves.cs             # écrivain : Ajouter(releve), Ajouter(evenement), Purger(now), DerniereEcriture
│       ├── JournalisationUsageProvider.cs# décorateur IUsageProvider : dédup t/source, événements, IHostedService
│       ├── LecteurJournal.cs             # E/S : mois chevauchant [de, à[, lecture tolérante
│       └── AnalyseReleves.cs             # PUR : série, trous, resets, Δ, sauts, bornes de semaine
└── App.xaml.cs                           # mutex avant Host ; DI du journal entre tête et composite
tests/Chronos.Tests/
├── TestData/transcript-multi-blocs.jsonl # fixture réelle anonymisée (CPT-01)
├── TestData/journal/*.jsonl              # fixtures de journal (JRN-05)
├── DedupMessagesTests.cs, JournalRelevesTests.cs, JournalisationUsageProviderTests.cs,
├── LecteurJournalTests.cs, AnalyseRelevesTests.cs, VerrouInstanceUniqueTests.cs,
└── GardesJournalTests.cs                 # gardes structurelles (aucun lecteur de "usage" hors helper, doc ↔ schéma)
```

**Piège de garde à traiter dans le premier plan qui crée un sous-dossier** (32-04) : `ServicesLayerPurityTests` filtre
`t.Namespace is "Chronos.Services" or "Chronos.Models"` (égalité stricte, l. 37) ; `GardesDoctrineTests` (l. 86, 167) et
`NormalisationUniqueTests` (l. 74) énumèrent `Directory.EnumerateFiles(d, "*.cs")` « dossiers plats, non récursif ». Un type sous
`Chronos.Services.Historique` ou un fichier sous `Services/Historique/` échappe à ces quatre gardes. Deux options : (a) namespace
`Chronos.Services`/`Chronos.Models` conservés même en sous-dossier + passage des énumérations en `SearchOption.AllDirectories` ;
(b) sous-namespace + `Namespace.StartsWith("Chronos.Services")`. Recommandé : **(b) + AllDirectories**, avec un test qui prouve
que la garde VOIT les nouveaux types (motif `La_garde_voit_bien_l_assembly_Chronos`).

### Pattern 1 : le décorateur de journalisation, entre la tête et le composite

**What :** `JournalisationUsageProvider : IUsageProvider, IHostedService` enveloppe le composite ; `LastExactUsageProvider`
l'enveloppe à son tour. Il délègue à l'inner, puis journalise ce que l'inner a produit d'exact, et rend le snapshot **inchangé**.
**Pourquoi là :** l'inner brut ne connaît ni le magasin (`Source = MagasinDernierExact` naît dans `LastExactStore.Reconstruire`,
donc AU-DESSUS) ni la doctrine (planchers et `EncoreValide` naissent dans `DoctrineFraicheur.Statuer`, appelée par la tête). Filtre
suffisant : `Reliability == Exact && Utilization is not null && CapturedAt is not null`. Le rejeu du cache de la sonde
(`ServirCacheOu`, l. 511-512 : même instance 4 fois sur 5) est éliminé par « `t` strictement supérieur au dernier `t` écrit pour cette
source ».
**Câblage (App.xaml.cs l. 400-410) :**
```csharp
// Source : App.xaml.cs l. 400-410 (câblage actuel) — le journal s'insère ICI, l'inner de la tête devient le journal.
services.AddSingleton(sp => new JournalisationUsageProvider(
    inner: new CompositeUsageProvider(/* chaîne inchangée */),
    journal: sp.GetRequiredService<JournalReleves>(),
    etatServeur: sp.GetRequiredService<IEtatServeur>(),   // sonde_refusee (transition de DernierResultat)
    authStatus: sp.GetRequiredService<IAuthStatus>(),     // jeton_invalide (EtatChange → Deconnecte)
    clock: sp.GetRequiredService<IClock>()));
services.AddHostedService(sp => sp.GetRequiredService<JournalisationUsageProvider>()); // AVANT RefreshOrchestrator
services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
    inner: sp.GetRequiredService<JournalisationUsageProvider>(), store: …, clock: …, activite: …));
```
Ordre des services hébergés : ils démarrent dans l'ordre d'inscription et s'arrêtent en ordre inverse → inscrit AVANT
`RefreshOrchestrator` (l. 422), le journal écrit `demarrage` avant le premier relevé et `arret` après le dernier
(`OnExit` → `_host.StopAsync()` l. 215 ; un kill ne produit pas d'`arret` : le lecteur en fait un trou « Chronos arrêté »).

**Deux fenêtres, deux `CapturedAt` ?** En production la sonde produit les deux fenêtres avec le même `now` (l. 431). Si le composite
mélange (5 h de la sonde, hebdo d'un repli), écrire **une ligne par couple (`CapturedAt`, `Source`) distinct** présent dans le
snapshot, chaque ligne ne portant que les fenêtres qui partagent ce couple (l'autre à `null`). C'est ce qui donne son sens à la clé
d'idempotence « `CapturedAt` + source » : une ligne = un relevé d'UNE source à UN instant.

### Pattern 2 : append exclusif avec relecture de queue (JRN-03)

**What :** une écriture = ouvrir `FileShare.None` (reprises : 60 essais × 25 ms, comme `EcritureEtatSession.EssaisMax`), relire les
derniers ≤ 16 Ko, trouver la dernière ligne de relevé de la même `source`, comparer `t`, écrire la ligne UTF-8 sans BOM terminée
par `\n`, `Flush`, fermer.
**Pourquoi :** mesuré le 2026-09-27 sur .NET 8.0.25 : `FileMode.Append` fait un `Seek(End)` à l'OUVERTURE, pas un `O_APPEND` ; deux
appenders concurrents ont produit `ligne-B|` seule. Le verrou exclusif rend la séquence lire-comparer-écrire atomique entre
processus ET entre threads du même processus (les événements `IAuthStatus.EtatChange` arrivent sur des threads du pool,
concurrents du consommateur de l'orchestrateur) — doubler d'un `lock` en mémoire pour ne pas payer les reprises.
**Une ligne < 4 Ko :** ~230 octets mesurés sur le format proposé → ≈ 66 Ko/jour (288 relevés), ≈ 2 Mo/mois, ≈ 24 Mo/an.

### Pattern 3 : lecture tolérante ligne par ligne (JRN-03, JRN-05)

Reprendre mot pour mot la boucle de `TranscriptActivityProvider.ReadAsync` (l. 61-83) : `FileShare.ReadWrite`, `ReadLineAsync`,
ligne vide ignorée, `JsonDocument.Parse` sous `catch (JsonException)`, **`v` lu en premier** (`v` absent ou ≠ 1 → ligne sautée, pas
le fichier), `ev` présent → événement, sinon relevé ; champ manquant → `null`, jamais 0 ; dernière ligne tronquée = une
`JsonException` = ignorée.

### Pattern 4 : mutex mono-instance avant le Host (CPT-03)

```csharp
// Source : App.xaml.cs — à placer APRÈS les court-circuits --statusline (l. 20), --hook (l. 29), --cadrans (l. 39),
// --sessions (l. 50) et AVANT Host.CreateApplicationBuilder() (l. 61). Ces modes restent multi-instances par construction.
private static Mutex? _verrou;   // champ STATIQUE : le GC ne doit jamais libérer le verrou pendant la vie de l'overlay
...
var verrou = VerrouInstanceUnique.Acquerir(@"Local\Chronos-overlay");   // Services/, neutre, testable
if (!verrou.Obtenu)
{
    MessageBox.Show("Chronos tourne déjà (une seule instance à la fois). Cette copie se retire ; l'autre continue.",
                    "Chronos", MessageBoxButton.OK, MessageBoxImage.Information);   // WPF autorisé ici : App.xaml.cs
    Shutdown(); return;
}
_verrou = verrou.Mutex;
```
Règles (docs Microsoft, `Mutex(bool, string, out bool)`) : avec `initiallyOwned: true`, le thread appelant possède le mutex
**seulement si `createdNew` est vrai** ; sinon tenter `WaitOne(0)` — un `AbandonedMutexException` (l'instance précédente est morte
sans libérer) signifie que l'on POSSÈDE maintenant le mutex → démarrer normalement. Préfixe `Local\` = session Windows courante
(pas de `Global\` : pas de droits requis, un autre utilisateur de la machine peut avoir le sien). Libérer dans `OnExit`
(`ReleaseMutex` sur le MÊME thread qui l'a acquis — le thread UI — puis `Dispose`). Les trois exe déjà en marche (3.1.0, 3.2.0,
3.2.1) ne connaissent pas ce mutex : le constat commence par les quitter.

### Pattern 5 : bornes de la semaine de forfait en heure locale (JRN-05)

```csharp
// Vérifié le 2026-09-27 sur .NET 8.0.25 (tz "Romance Standard Time") :
//   samedi 2026-09-19 00:00 local = 2026-09-18T22:00Z (= resets_at 7 j constaté)
//   samedi 2026-10-24 00:00 local = 2026-10-23T22:00Z ; samedi 2026-10-31 00:00 local = 2026-10-30T23:00Z → semaine de 169 h
//   WeeklyRecalibration.NextReset (7 × 24 h fixes depuis 2026-07-11T00:00+02:00) → 2026-10-30T22:00Z : UNE HEURE de dérive.
public static (DateTimeOffset Debut, DateTimeOffset Fin) SemaineDeForfait(DateTimeOffset instant, DateTimeOffset? resetHebdoObserve,
                                                                          DateTimeOffset? ancre, TimeZoneInfo tz)
{
    // Repère : le prochain reset hebdo OBSERVÉ dans le journal (r7 du dernier relevé) ; repli : ancre WeeklyAnchor ; repli : samedi.
    var repere = resetHebdoObserve ?? ancre ?? instant;
    var localRepere = TimeZoneInfo.ConvertTime(repere, tz);
    // Minuit LOCAL du jour du repère, puis recul au samedi ; l'arithmétique se fait sur le CALENDRIER local, pas en TimeSpan.
    var minuit = new DateTime(localRepere.Year, localRepere.Month, localRepere.Day, 0, 0, 0, DateTimeKind.Unspecified);
    while (minuit.DayOfWeek != DayOfWeek.Saturday) minuit = minuit.AddDays(-1);
    DateTimeOffset Utc(DateTime local) => new(local, tz.GetUtcOffset(local));
    var fin = Utc(minuit);
    while (fin <= instant) fin = Utc(minuit = minuit.AddDays(7));      // AddDays sur DateTime local : 169 h le 25/10, 167 h le 28/03
    while (Utc(minuit.AddDays(-7)) > instant) fin = Utc(minuit = minuit.AddDays(-7));
    return (Utc(minuit.AddDays(-7)), fin);
}
```
`tz` est un paramètre (défaut `TimeZoneInfo.Local` au site d'appel) : les tests passent `FindSystemTimeZoneById("Romance Standard
Time")` (Windows) avec repli `"Europe/Paris"` (IANA, accepté sur cette machine).

### Anti-Patterns to Avoid
- **Lire un fichier de `%APPDATA%\Chronos` depuis une session pour juger l'overlay** : c'est la copie du paquet. Toute
  vérification passe par la sonde WMI (VAL-04) ou par le diagnostic de l'overlay lui-même.
- **Prendre la PREMIÈRE ligne d'un `message.id`** : sous-compte `output_tokens` (streaming). Prendre le max par champ.
- **Dédup par fichier pour la question « tokens depuis T »** : 491 ids sur 8 jours vivent dans 2 à 3 fichiers.
- **`FileMode.Append` + `FileShare.ReadWrite` pour l'idempotence** : perd des lignes (mesuré).
- **Recopier `300`/`10 min` en dur** : dériver de `RateLimitHeaderUsageProvider.CadenceNominale` (précédent `DoctrineFraicheur.LimiteAge`).
- **Un `catch { }` autour d'une écriture** : le précédent de cette phase montre qu'il cache aussi bien une panne qu'une absence de panne.
- **`Process.GetProcessesByName("Chronos")`** : le nom réel est `Chronos-v3.2.2` ; filtrer par préfixe sur `Process.GetProcesses()`.
- **Sous-dossier sous `Services/` sans élargir les gardes** : les types neufs sortiraient de la surveillance.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Append multi-processus sûr | Un « append » maison avec `FileMode.Append` | `FileShare.None` + reprises bornées (motif `EcritureEtatSession.EcrireAvecReprise`) | `Append` = seek à l'ouverture ; mesuré : perte de ligne |
| Sérialisation d'une ligne | Concaténation de chaînes JSON | `JsonSerializer.Serialize(record, options)` avec `JsonStringEnumConverter`, `WhenWritingNull` | Échappement, culture (`0,01` vs `0.01` : piège fr-FR déjà rencontré, `UsageNormalization`) |
| Mots de l'âge | Nouveau formateur | `LibelleSource.Anciennete(t, now)` | Mêmes paliers partout (« il y a 4 min » du diagnostic) |
| Détection de la vue virtualisée | Nouvelle heuristique | `Directory.Exists(Path.Combine(APPDATA, "Claude"))` = vue virtualisée (règle de `docs/desktop-app-sessions.md` §7 et des sondes 29/31) + `RacinesEtat.Candidats` pour nommer le paquet | Déjà prouvé sur cette machine |
| Bornes de semaine avec DST | Arithmétique en `TimeSpan` de 7 j | `DateTime` local + `AddDays` + `tz.GetUtcOffset` | La dérive d'une heure de `WeeklyWindow`/`NextReset` est justement l'hypothèse à vérifier |
| Mutex robuste | Fichier-verrou PID | `Mutex` nommé `Local\…` (libéré par l'OS à la mort du processus, `AbandonedMutexException` = acquis) | Un fichier-verrou survit au crash et bloquerait le redémarrage |

**Key insight :** tout ce dont la phase a besoin existe déjà dans le dépôt sous forme éprouvée ; le travail est de brancher juste,
pas d'inventer.

## Findings par exigence

### CPT-01 — dédup par `message.id`

**Cartographie des lecteurs de `message.usage` (grep sur `src/Chronos/Services`, 2026-09-27) :** un seul —
`TranscriptActivityProvider.SumUsageTokens` (l. 136-144), appelé l. 79 dans la boucle de `ReadAsync`, qui ajoute `(when, tokens)` à
`entries` PAR LIGNE. `DoctrineFraicheur.TokensDepuisReleve` ne lit aucun JSON : il reçoit `TranscriptActivityLog.Since(t).Tokens`
(`ITranscriptActivitySource.cs` l. 60-73) — il « hérite » donc de la correction sans code propre dès que `entries` est dédoublonné.
`TranscriptSessionSource` et `TravailSousAgent` lisent des transcripts mais pas `usage`. Parsing : `System.Text.Json`
`JsonDocument.Parse(line)` + `TryGetProperty`, streaming `FileShare.ReadWrite`, filtre mtime 8 jours (`HorizonSpan`).

**Faits mesurés (lecture seule, 8 derniers jours, 934 fichiers, 1,28 Go, 33 s en Python) :**
- 128 545 lignes `assistant`, 60 349 `message.id` distincts (facteur 2,13), **0 ligne sans `message.id`**, `requestId` présent sur
  3 247 / 3 248 lignes du plus gros fichier → le repli `requestId` reste un filet, pas un chemin nominal.
- **12 835 ids sur 24 376 (400 fichiers les plus récents) ont des `usage` divergents entre leurs lignes ; le SEUL champ qui diverge
  est `output_tokens`, toujours monotone croissant, la dernière ligne étant toujours le max** (12 835 / 12 835). Versions Claude Code
  concernées : 2.1.260, 2.1.270, 2.1.275, 2.1.280, 2.1.281 ; le fichier d'août (2.1.247) n'en a aucun. Sommes des 4 champs :
  toutes lignes 14,05 G ; première ligne 6,66 G ; dernière ligne = max par champ 6,68 G → **facteur 2,1**.
- **491 ids présents dans 2 à 3 fichiers** (ex. trois transcripts du même projet) — reprise/fork de session.
- Le même fichier contient parfois des lignes STRICTEMENT identiques (même `timestamp`, même bloc) en double : la dédup par id les
  absorbe aussi.

**Règle à implémenter :** helper unique, pur, `DedupUsage` (nom libre) : `Ajouter(id, timestamp, usage)` accumule par id le **max de
chaque champ** (équivalent « dernière ligne » mais indépendant de l'ordre d'écriture) et le **premier timestamp** vu (le message est
facturé une fois ; l'instant de la première ligne est celui de la réponse) ; `id = message.id ?? requestId ?? null` ; sans aucun id
→ compter la ligne telle quelle (jamais jeter un token connu). Le `HashSet`/dictionnaire est **partagé sur toute la passe** de
`ReadAsync` (60 k entrées × ~40 o = quelques Mo, acceptable au regard des 536 Mo lus) ; le helper accepte le dictionnaire de
l'extérieur pour que la phase 33 puisse le scoper par fichier si elle le décide.

**Fixture réelle anonymisée (gabarit extrait du plus gros transcript, contenu textuel remplacé, ids remplacés) :** trois lignes
`assistant` du même `msg_…`, blocs `thinking` / `text` / `tool_use`, `usage` dont `output_tokens` vaut 8, 8 puis 256 (schéma
observé le 2026-09-26 23:56 en 2.1.281), les trois autres champs identiques ; plus une 4e ligne dupliquée à l'identique de la 1re.
Clés de premier niveau réelles : `parentUuid, isSidechain, message{model,id,type,role,content[],stop_reason,stop_sequence,usage},
requestId, type, uuid, timestamp, effort, userType, entrypoint, cwd, sessionId, version, gitBranch`. Clés `usage` réelles :
`input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens, output_tokens_details, server_tool_use,
service_tier, cache_creation, inference_geo, iterations, speed`. Placeholders : `<cwd>`, `<sessionId>`, `<uuid>`,
`msg_01EXEMPLE…`, `req_01EXEMPLE…`, textes `"…"`. Attendu du test : `Since(T)` = `input + max(output) + cache_w + cache_r` d'UN
message (ex. 2 + 256 + 35 005 + 41 741), pas trois fois, pas 2 + 8 + ….

**Garde « aucun lecteur sans dédup » (structurelle, textuelle, motif `GardesDoctrineTests`) :** balayer `Services/**/*.cs` et
`Models/**/*.cs` ; toute occurrence de `"usage"` (chaîne JSON) ou de `"output_tokens"` hors du fichier du helper rougit, avec un
message qui nomme le helper. Compléter par une garde réflexive : le helper existe et `TranscriptActivityProvider` ne contient plus
de méthode `SumUsageTokens`. Le test doit prouver qu'il voit ≥ 40 fichiers (anti-mutisme, l. 91-93 de `GardesDoctrineTests`).

### CPT-02 — le « gel » de `last-exact.json`

**Chaîne lue :** `RefreshOrchestrator.ExecuteAsync` (consommateur unique, `GetAsync` sérialisés) → `LastExactUsageProvider.GetAsync`
(l. 257-309) : `if (FiveHour.Exact || SevenDay.Exact) _store.Save(snap)` dans un `try { … } catch { memorise = null; dejaEuUnExact = null; }`
→ `LastExactStore.Save` (l. 67-90) : `LireBrut()` (tolérant, ne lève jamais), `Convertir` exige `Exact && Utilization && ResetsAt &&
CapturedAt` (l. 150-154), `WriteAllText(tmp)` puis `File.Move(tmp, path, overwrite: true)`. La sonde produit bien les trois champs
(`RateLimitHeaderUsageProvider.LireFenetre` l. 422-437 : `CapturedAt = now`, `Source = SondeEnTetes`). Le diagnostic (`chronos.log`
du 26/09 22:34, version 3.2.1) montre « EXACT — 12 % · sonde d'en-têtes · relevé il y a 4 min · frais » : `Save` est donc appelé
avec des fenêtres convertibles à chaque tick.

**Fait qui tranche (sonde WMI du 2026-09-27 01:56:15, parent `WmiPrvSE.exe`, `%APPDATA%\Claude` absent) — vue RÉELLE :**

| Fichier | Taille | Dernière écriture |
|---|---|---|
| `last-exact.json` | 326 o | **2026-09-27 01:55:37.322** (contenu : `captured_at 2026-09-26T23:53:37Z`, u5 0,01, r5 `2026-09-27T04:50Z`, u7 0,41, r7 `2026-10-02T22:00Z`) |
| `settings.json` | 536 o | 2026-09-27 01:33:39 |
| `treated.json` | 319 o | 2026-09-27 01:09:23 |
| `oauth.dat` | 518 o | 2026-09-27 01:04:36 |
| `chronos.log` | 6 599 o | 2026-09-26 22:38:48 (rapport daté 22:34:38, 3.2.1) |
| `.tmp-*` | — | aucun ; `historique\` absent ; ACL `Tanguy:(F)` ; attribut `A` seul |

Le fichier est réécrit chaque minute (mtime = capture + 2 min, cadence 60 s), contenu = cache de la sonde rejoué. **Il n'y a pas de
gel.** Ce que l'agent a lu depuis les sessions est `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Chronos\
last-exact.json` (326 o, 2026-09-13 12:44:59.705, `captured_at 2026-09-13T10:41:59Z`), copie COW du paquet : la même vue contient
`settings.json` et `archived.json` du 13/09 07:36:58 (un overlay a démarré SOUS l'arbre de l'app ce matin-là : purge `archived.json`
+ écriture des réglages), `treated.json` 12:25:31, puis `last-exact.json` 12:44:59 = son dernier `Save` avant d'être quitté.
`chronos.log`, lui, n'a PAS de copie dans le paquet : la vue fusionnée le lit en pass-through, d'où un rapport « frais » à côté d'un
`last-exact.json` « figé » — l'incohérence qui a fondé l'hypothèse du gel.

**Ce que CPT-02 devient (à écrire tel quel dans le SUMMARY de 32-02 et dans `docs/data-sources.md`) :**
1. *Cause établie et datée* : virtualisation AppData du paquet MSIX ; copie COW du 2026-09-13 12:44:59 ; vue réelle saine (sondes du
   25/09 21:17 et du 27/09 01:56). Le `try/catch` de `LastExactUsageProvider` n'a rien avalé.
2. *Correction d'observabilité, quand même due* : `LastExactStore` expose `DateTimeOffset? DerniereEcriture` (posée après le `Move`,
   amorcée au démarrage par `File.GetLastWriteTimeUtc` si le fichier existe) et `string? DerniereErreur` (`type : message` de la
   dernière exception, effacée au succès) ; `LastExactUsageProvider` n'avale plus : il consigne `store.DerniereErreur` et, si le journal
   est injecté, écrit un événement `ecriture_ratee` (`{v, t, ev: "ecriture_ratee", magasin: "last-exact", cause}` — extension du
   vocabulaire, lecteur tolérant). Le rendu du snapshot reste identique (pas de crash, EXA-05 conservé : `null`, pas `false`).
3. *Diagnostic* : nouvelle section `[Magasins persistants]` — pour `last-exact.json`, `historique\releves-AAAA-MM.jsonl`, et
   « agrégats de tokens : aucun (phase 33) » : chemin, existe ?, taille, « dernière écriture il y a N min » (`LibelleSource.Anciennete`),
   dernière erreur ; plus une ligne d'en-tête **« Vue AppData : réelle »** ou **« virtualisée (paquet Claude_…) — les fichiers lus ici
   ne sont pas ceux de l'overlay lancé par l'Explorateur »** (`Directory.Exists(APPDATA\Claude)` + `RacinesEtat.Candidats`).
4. *Test qui reproduit « la panne »* : magasin dont le dossier parent est un FICHIER (motif exact de
   `Un_magasin_en_panne_ne_leve_pas_et_n_affirme_rien_sur_le_passe`, l. 230-255) → `DerniereErreur` non nulle, événement
   `ecriture_ratee` dans le faux journal, ligne de diagnostic « ÉCHEC », snapshot rendu identique. Et un test de non-régression :
   magasin sain → `DerniereEcriture == clock.UtcNow` après `Save`.

**Note :** `File.Move(tmp, path, overwrite:true)` peut lever `UnauthorizedAccessException` « failed to acquire an exclusive access to
the destination file » quand un lecteur tient la cible sans `FILE_SHARE_DELETE` (docs Microsoft ; `EcritureEtatSession` l. 38-45 a
mesuré 290 pertes sur 500 face à un lecteur toutes les 2 s). Ici la cible n'est relue que par `LireBrut` (fugace) et le diagnostic :
une collision est transitoire et sera désormais VISIBLE (`DerniereErreur`), plus muette.

### CPT-03 — mono-instance

- **Démarrage réel (App.xaml.cs) :** `--statusline` (l. 20-25, `Environment.Exit(0)`), `--hook` (l. 29-34, `Environment.Exit(code)`),
  `--cadrans` (l. 39-46) et `--sessions` (l. 50-57) sortent AVANT `base.OnStartup` + `Host.CreateApplicationBuilder()` (l. 59-63). Le
  mutex se pose entre l. 57 et l. 61 : les modes CLI et hooks ne le voient jamais (multi-instances par construction : Claude Code
  lance jusqu'à 5 hooks en parallèle, `EcritureEtatSession` l. 117-119).
- **Message :** `MessageBox.Show` WPF depuis `App.xaml.cs` (déjà un fichier WPF ; aucune règle de pureté ne s'y applique) — pas de
  fenêtre dédiée. Texte proposé : « Chronos tourne déjà (une seule instance à la fois). Cette copie se retire ; l'autre continue.
  Pour changer de version : réglages → « Quitter Chronos » sur le cadran, puis relance. » Puis `Shutdown()` sans avoir construit le
  Host (aucun `IHostedService` démarré, aucune réconciliation de `~/.claude/settings.json`, aucun `chronos.log` écrasé).
- **Classe neutre testable :** `VerrouInstanceUnique.Acquerir(nom)` rend `(bool Obtenu, Mutex? Mutex, bool Abandonne)` ; tests : (1)
  deux acquisitions du même nom sur DEUX threads (`Thread`, pas `Task` : la propriété d'un mutex est par thread) → la seconde échoue ;
  (2) libération → la seconde réussit ; (3) nom unique par test (`Local\Chronos-test-<guid>`) pour l'isolation ; (4) simulation
  d'abandon : un thread acquiert et se termine sans libérer → `Acquerir` rend `Obtenu = true, Abandonne = true`.
- **« N processus Chronos » :** `Process.GetProcesses()` filtré `ProcessName.StartsWith("Chronos", OrdinalIgnoreCase)` (vérifié sans
  admin le 2026-09-27 : 3 overlays + 2 `Chronos-v3.2.1` éphémères = hooks en cours). Écrire : nom#pid, âge (`StartTime` sous
  `try/catch` — peut lever pour un processus d'un autre utilisateur), et marquer « (dont N de moins de 10 s : hooks probables) » ;
  exclure le PID courant du compte des « autres ». Ajouter « verrou mono-instance : tenu par ce processus / tenu par un autre / libre »
  via `Mutex.TryOpenExisting`.
- **Réalité du constat :** trois exe tournent (3.1.0 PID 40772 depuis le 23/09 11:09, 3.2.0 PID 126160 depuis le 26/09 14:18, 3.2.1
  PID 121900 depuis le 26/09 22:34, tous parent `explorer` 27872) et écrivent tour à tour `last-exact.json`, `treated.json`,
  `chronos.log`. Le mutex de la 3.2.2 ne les arrête pas ; le point (a) du constat les fait quitter par l'utilisateur.

### JRN-01 — ce qui entre dans le journal

- Source des champs, par fenêtre `WindowState` de l'inner brut : `Utilization` → `u5/u7` (fraction 0..1 telle que reçue :
  `UsageNormalization.FractionDepuisTexteFraction`, précision 0,01 constatée — écrire le `double` tel quel, sans arrondi, pour que
  l'hypothèse de granularité se vérifie AVEC le journal), `ResetsAt` → `r5/r7` (ISO 8601 `O`, UTC), `StatutServeur` → `statut5/statut7`
  (`Autorise | AutoriseAvertissement | Rejete | NonReconnu`, noms d'enum, `null` si non rapporté), `Depassement` → `overage`
  (`Utilization` en dépassement, nombre ou `null` ; ajouter `overage_statut` optionnel — le cas « politique seule » du 2026-09-12 a
  `EstRenseigne` sans `EstEnCours`), `Source` → `source` (`SondeEnTetes | EndpointOAuthChronos | EndpointOAuthClaude | PontStatusLine`).
  `IEtatServeur.Depassement` (canal latéral) n'est pas nécessaire pour la ligne : le champ voyage par référence sur la fenêtre (l. 434).
- `t` = `CapturedAt` de la fenêtre (UTC). Le décorateur garde en mémoire `dernierT[source]` amorcé à la première écriture par la
  relecture de queue ; la relecture sous verrou reste la vérité pour l'idempotence multi-processus.
- N'entrent jamais : `Reliability != Exact` (planchers `Estimated`, `Unavailable`), `Source == MagasinDernierExact` (impossible sous
  la tête, mais l'exclusion est écrite et testée), un `CapturedAt` ≤ dernier écrit de la même source (rejeu de cache, l. 511-512).

### JRN-02 — événements

| `ev` | Déclencheur | Où | Note |
|---|---|---|---|
| `demarrage` | `IHostedService.StartAsync` du décorateur | avant `RefreshOrchestrator` (ordre d'inscription) | porte `version` (lue comme `DiagnosticService.VersionEmbarquee`) : utile pour dater un changement de comportement |
| `arret` | `StopAsync` (`OnExit` → `_host.StopAsync()` l. 215) | après l'orchestrateur (ordre inverse) | absent sur kill/veille : le lecteur produit alors un trou « Chronos arrêté » sans `arret` |
| `jeton_invalide` | `IAuthStatus.EtatChange` → `EtatAuthentification.Deconnecte` (transition uniquement, `ChronosTokenAuthority.Publier` l. 235-240) | abonnement dans le décorateur | thread du pool → passer par le `lock` de l'écrivain |
| `sonde_refusee` | après `inner.GetAsync`, `IEtatServeur.DernierResultat` passe à `SaturationEnTetesLus`, `SaturationSansEnTetes` ou `RefusServeur`, OU une fenêtre exacte porte `StatutServeur.Rejete` | sur TRANSITION (mémoriser le dernier état) | un 429 « porteur » produit AUSSI un relevé (les en-têtes survivent au refus, HDR-02) : les deux lignes coexistent |
| `reprise` | l'écrivain constate `t − dernierT[source] > 2 × CadenceNominale` (10 min) au moment d'écrire un relevé | calculé, pas observé | dit au lecteur « le trou finit ici » sans qu'il ait à deviner |
| `ecriture_ratee` (extension CPT-02) | échec de `LastExactStore.Save` | `LastExactUsageProvider` | `magasin`, `cause` |

Le lecteur ignore tout `ev` inconnu (tolérance) et le compte comme « événement non reconnu » pour le diagnostic.

### JRN-03 — atomicité, idempotence, tolérance, rotation

- **Écriture :** voir Pattern 2. Nom de fichier `releves-{t:yyyy-MM}.jsonl` avec `t` en **UTC** ; `Directory.CreateDirectory(HistoriqueDir)`
  à chaque écriture (dossier supprimé à chaud = recréé). UTF-8 sans BOM, `\n` seul. Une écriture qui échoue après les reprises pose
  `DerniereErreur` et n'est pas relancée (le prochain relevé arrive dans 5 min).
- **Idempotence :** clé `(t, source)` ; deux processus → chacun relit la queue sous le verrou ; test « deux écrivains, 200 écritures »
  : deux `Task.Run` (threads du pool) écrivant la MÊME séquence de 100 relevés (ou 200 entremêlés) dans un même dossier temp via deux
  instances distinctes de `JournalReleves` (pas de partage d'état mémoire) → le fichier contient exactement 100 lignes distinctes, 0
  doublon, 0 ligne tronquée, `FileShare.None` s'appliquant aussi entre handles du même processus. Un test croisé « le rejeu du cache
  n'écrit pas » avec un `FakeUsageProvider` qui rend la même instance 6 fois → 1 ligne.
- **Tolérance :** fixture avec ligne tronquée en milieu de JSON, ligne `{"v":2,…}`, ligne `{"v":1}` sans `t`, ligne vide, ligne
  d'un `ev` inconnu → seules les lignes valides sortent, aucune exception, compteur `LignesIgnorees` exposé.
- **Rotation/rétention :** mensuelle par construction du nom ; à `StartAsync` (et une fois par jour au plus, via le tick de 60 s et
  `IClock`) supprimer `releves-AAAA-MM.jsonl` dont `AAAA-MM` < mois courant − 24 ; noms non conformes ignorés ; suppression en échec
  comptée, pas relancée (motif `BalayageMagasinSessions`). Aucune compaction.
- **Neutralité :** aucun `System.Windows.*` ; `ServicesLayerPurityTests` élargi (voir carte des fichiers).

### JRN-04 — âge de la dernière écriture

- **Où vit le texte des réglages :** `SettingsWindow` n'a pas de code-behind métier (`SettingsWindow(MainViewModel)`) ; la ligne d'état de
  la sonde est `TexteEtatSonde`/`AfficherEtatSonde` de `MainViewModel` (`MajTexteEtatSonde`, l. 403-417), bindée par `LigneEtatSonde`
  (SettingsWindow.xaml l. 195-198) sous la carte « Sonde d'en-têtes » de la section DONNÉES (l. 156). Ajouter une carte minimale
  « Journal des relevés » sur le même gabarit (`Border Panel2`, `TextBlock Ink 13`, sous-texte `Ink2 10.5`) avec `TexteEtatJournal`
  (« dernière écriture il y a N min · N relevés aujourd'hui ») et une pastille `Ellipse Fill={DynamicResource Alerte}` (jeton existant
  `#EFA23A`, DesignTokens.xaml l. 22, déjà utilisé par `PastilleDeconnexion` MainWindow.xaml l. 300-310) visible si `AlerteJournal`.
  La carte complète F1 arrive en phase 35 : ne pas la construire ici.
- **Alimentation du VM :** injecter un contrat neutre `IEtatJournal { DateTimeOffset? DerniereEcriture; string? DerniereErreur; int RelevesDuJour; }`
  (implémenté par `JournalReleves`) en paramètre optionnel de fin de constructeur (motif `IEtatServeur? etatServeur = null`, l. 244) ;
  recalcul dans `ApplySnapshot` (tick 60 s suffit : le seuil est 15 min) — pas d'événement supplémentaire.
- **Règle des 15 min :** `alerte = now − max(DemarrageDuProcessus, DerniereEcriture ?? DemarrageDuProcessus) > 15 min` — sinon au
  démarrage l'alerte s'allumerait sur une dernière écriture d'hier, ce qui n'est pas « muet alors que Chronos tourne ». Texte :
  « journal muet depuis N min » (vocabulaire du plan de design). Le seuil est une constante nommée dérivée : `3 × CadenceNominale`.
- **Test avec `IClock` :** `FakeClock` avancé de 16 min après une écriture à `t0` → `AlerteJournal == true` ; avancé de 16 min sans
  écriture mais processus démarré il y a 5 min → `false` ; écriture → `false`.
- **Diagnostic :** même chiffre, même mot, dans `[Magasins persistants]` (voir CPT-02).

### JRN-05 — lecture par plage en classes pures

- **Entrée :** `LecteurJournal.Lire(de, à)` (E/S : mois UTC chevauchant `[de, à[`, lecture tolérante, tri par `t`) rend
  `(IReadOnlyList<ReleveJournal> Releves, IReadOnlyList<EvenementJournal> Evenements, int LignesIgnorees, DateTimeOffset? JournalOuvertLe)`
  (`JournalOuvertLe` = `t` de la première ligne du plus ancien fichier existant : « journal ouvert le … » du plan de design).
- **Analyse pure (`AnalyseReleves`, aucune E/S, `now` et `cadence` en paramètres) :**
  - `Serie` : les relevés triés (par source si plusieurs ; la vue en retient une, la sonde).
  - `Trous` : `t[i] − t[i−1] > 2 × cadence` → `Trou(Debut = t[i−1], Fin = t[i], Cause)` ; `Cause` = le dernier événement dans
    `]t[i−1] − cadence, t[i]]` parmi `arret` → « Chronos arrêté », `jeton_invalide` → « jeton invalide », `sonde_refusee` →
    « sonde refusée » ; sans événement → « Chronos arrêté » si un `demarrage` tombe dans le trou, sinon « cause inconnue » (jamais tu).
    Un trou ouvert à la fin (dernier relevé > 2 cadences avant `now`) est rendu avec `Fin = null`.
  - `Resets5h` / `ResetsHebdo` : `r5[i] != r5[i−1]` (resp. `r7`) → instant `r5[i−1]` (le reset observé est l'ancienne borne).
  - `Deltas` : entre relevés CONSÉCUTIFS (pas de trou entre eux) de MÊME `r5` (resp. `r7`) : `u[i] − u[i−1]` ; un Δ négatif sans
    changement de `r` est conservé et marqué `Anormal` (c'est l'hypothèse « Δ = consommation » qui se teste) ; jamais à travers un reset.
  - `SautsNonLocalises` : de part et d'autre d'un trou, si `r` identique : `u[après] − u[avant]` avec `Repartition = Inconnue` ; si `r`
    a changé pendant le trou : `null` (« au moins un reset dans l'absence »).
  - `BornesSemaineDeForfait`, `BornesJour(tz)`, `QuatreSemaines` (4 × semaine de forfait consécutives finissant par la courante) —
    Pattern 5 ; repli `WeeklyAnchor` de `ChronosSettings` (l. 61) quand le journal n'a aucun `r7`.
- **Fixtures de journal (TestData/journal/) :** (1) journée nominale 288 relevés ; (2) trou « arrêté » avec `arret`/`demarrage` ; (3)
  trou « jeton » (`jeton_invalide` puis `reprise`) ; (4) reset 5 h au milieu d'une heure (Δ coupé) ; (5) deux resets hebdo
  (`r7` change deux fois) ; (6) semaine du 24 au 31 octobre 2026 (169 h) ; (7) fichier mensuel à cheval (relevés du 30/09 23:58Z et du
  01/10 00:03Z dans deux fichiers).
- **Fuseaux :** `TimeZoneInfo` injecté ; tests en « Romance Standard Time » avec repli `"Europe/Paris"` ; dates de bascule 2026-10-25
  03:00 → 02:00 et 2027-03-28 02:00 → 03:00.

### JRN-06 — release 3.2.2

Procédure de 31-02 (SUMMARY, `docs/publish.md` §2, §6, §7), à recopier en tâches :
1. `src/Chronos/Chronos.csproj` : `Version 3.2.2`, `FileVersion 3.2.2.0`, `AssemblyVersion 3.2.2.0`, `InformationalVersion 3.2.2`
   (4 lignes, 0 CR, `3.2.1` × 0) ; `VersionPublieeTests` tient la cohérence csproj ↔ assembly.
2. `dotnet test Chronos.sln -c Debug --nologo -v q` deux fois (0 échec).
3. `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` → sortie
   `src/Chronos/bin/Release/net8.0-windows/win-x64/publish/Chronos.exe` (+ `.pdb`), **0 DLL**, < 120 Mo (≈ 77,4 Mo mesurés).
4. Copier à la racine du dépôt PRINCIPAL sous `Chronos-v3.2.2.exe` ; `git check-ignore -v` → `.gitignore:22:/Chronos-v*.exe` ; md5 et
   VersionInfo (`FileVersion 3.2.2.0 / ProductVersion 3.2.2 / ProductName Chronos`) relevés dans le SUMMARY ; ne pas supprimer les
   exe précédents (les sessions ouvertes les appellent).
5. Smoke sans overlay : `"…/Chronos-v3.2.2.exe" --hook SessionStart < /dev/null` → code 0, md5 de `~/.claude/settings.json` identique
   avant/après, 0 processus résident. (Seule écriture attendue : `%LOCALAPPDATA%\Temp\.net\Chronos-v3.2.2\`, hors périmètre.)
   **Le smoke ne prouve rien du mutex ni du journal** (mode `--hook`) : c'est le constat qui les prouve.
6. `docs/data-sources.md` : nouvelle section **« ## 7. Journal d'historique »** (après le §6 « Les sources du widget de sessions »,
   avant la ligne de fin datée) : chemin, schéma (deux formes de ligne, champs, unités, UTC), dédup et clé d'idempotence, événements
   (tableau ci-dessus, `ecriture_ratee` compris), rotation/rétention, **deux vues d'AppData** (renvoi §6 et
   `desktop-app-sessions.md` §7, avec la date du 2026-09-27 et le constat de la copie COW du 13/09), et les **trois hypothèses à
   vérifier avec le journal** (granularité 0,01 ; Δ = consommation, pas de recalcul rétroactif hors reset ; reset hebdo à l'heure
   locale le 25/10/2026 — attendu `2026-10-30T23:00Z` en local, `NextReset` dirait `22:00Z`). Garde documentaire
   (motif `ContratHooksDocumenteTests` : chemin `docs/` injecté par `AssemblyMetadata("CheminDocsChronos")`) : le §7 existe, nomme
   chaque champ du record de relevé et chaque valeur de l'enum d'événement — comparés par réflexion aux types réels.
7. `docs/publish.md` §7 : remplacer « il n'existe aucun verrou mono-instance » par le comportement 3.2.2 (message et retrait).
8. Commit `release: Chronos 3.2.2 - …` contenant exactement le csproj et les docs touchées ; pas d'étiquette, pas de push ; l'overlay
   n'est jamais lancé.

### VAL-04 — le constat

- **Protocole :** `31-CONSTAT.md` mot pour mot (règles, points (a), (b), (c), tableaux L1…Q et V01…V12) ; verdicts dans `32-CONSTAT.md`.
  Point (a) 3.2.2 : quitter 3.1.0 (PID 40772), 3.2.0 (126160), 3.2.1 (121900) par réglages → « Quitter Chronos » ; lancer
  `Chronos-v3.2.2.exe` par l'Explorateur ; **nouveau geste CPT-03** : double-cliquer une seconde fois → le message « tourne déjà » et un
  seul cadran ; réconciliation attendue : sauvegarde de md5 = état précédent, 9 remplacements `Chronos-v3.2.1.exe` → `Chronos-v3.2.2.exe`
  (8 hooks + statusLine), 3 `gsd-*` intacts.
- **Sonde hors arbre (modèle reproduit pendant cette recherche) :** un `.ps1` sous `%USERPROFILE%\Documents\chronos-constat\` lancé par
  `Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<ps1>"' }`
  (`ReturnValue = 0`), qui écrit son résultat dans le même dossier ; première ligne de preuve : `Test-Path $env:APPDATA\Claude` = **False**
  et parent = `WmiPrvSE.exe`. Relevés à y mettre : `Get-CimInstance Win32_Process -Filter "Name like 'Chronos%'"` (pid, parent,
  `CreationDate`, `CommandLine`) ; listing `%APPDATA%\Chronos` et `%APPDATA%\Chronos\historique` (taille, mtime) ; dernières lignes de
  `releves-2026-MM.jsonl` ; `Get-Content` de `last-exact.json` ; md5 de `~/.claude/settings.json`. Les fichiers de sonde sont supprimés
  après copie des valeurs dans `32-CONSTAT.md` (le dossier reste vide, comme en phase 31).
- **« Le journal s'écrit » :** 10 min après le lancement, mtime de `releves-2026-MM.jsonl` < 6 min ET ≥ 2 lignes de relevé distinctes
  (sonde 300 s) ET `demarrage` en première ligne du jour ; à l'heure : ≈ 12 relevés. Puis, à la demande de l'utilisateur, le rapport
  « Diagnostic… » collé : section `[Magasins persistants]` avec « dernière écriture il y a N min » et « Vue AppData : réelle ».
- **Anonymisation :** `%USERPROFILE%`, identifiants tronqués à 8 caractères, aucun titre de session, `grep -ci tanguy` = 0.

## Common Pitfalls

### Pitfall 1 : la vue virtualisée (MSIX) prise pour la vérité
**What goes wrong :** tout processus lancé sous l'app bureau Claude (sessions, hooks, `dotnet test`, l'agent) lit/écrit
`%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\…` en croyant lire `%APPDATA%`. Les copies COW y sont figées à la
date de la dernière écriture d'un processus virtualisé (13/09 pour `last-exact.json`).
**Why :** virtualisation AppData des paquets MSIX ; la vue fusionnée superpose la copie du paquet au fichier réel.
**How to avoid :** ne jamais conclure depuis une session ; sonde WMI ; ligne « Vue AppData » dans le diagnostic ; l'écrire dans
`data-sources.md` §7.
**Warning signs :** `Directory.Exists(%APPDATA%\Claude)` vrai ; mtimes anciens à côté d'un `chronos.log` frais.

### Pitfall 2 : `output_tokens` partiel par bloc
**What goes wrong :** dédup « première ligne gagne » sous-compte la sortie ; somme de toutes les lignes surcompte ×2,1.
**How to avoid :** max par champ par `message.id`, id partagé sur toute la passe ; fixture avec 8 / 8 / 256.
**Warning signs :** un test qui asserte le `usage` de la première ligne.

### Pitfall 3 : `FileMode.Append` n'est pas atomique
Mesuré : perte de ligne avec deux appenders. Verrou exclusif + relecture de queue ; test à deux écrivains.

### Pitfall 4 : gardes qui ne voient pas les sous-dossiers
`ServicesLayerPurityTests` (namespace exact), `GardesDoctrineTests` ×2 et `NormalisationUniqueTests` (« dossiers plats ») : élargir
dans le plan qui crée `Services/Historique/`, avec un test qui prouve qu'un type du sous-namespace est bien balayé.

### Pitfall 5 : mutex et thread de libération
`ReleaseMutex` doit être appelé par le thread propriétaire (UI) ; garder la référence dans un champ statique ; `initiallyOwned: true`
ne donne la propriété que si `createdNew` ; `AbandonedMutexException` = acquis, pas échec. Tester sur deux `Thread` distincts.

### Pitfall 6 : `Process.GetProcessesByName` et les hooks éphémères
Le nom du processus est `Chronos-v3.2.2` ; les hooks apparaissent 100 ms ; filtrer par préfixe, dater par `StartTime`, annoncer
« dont N de moins de 10 s ».

### Pitfall 7 : l'alerte « journal muet » au démarrage
Comparer à `max(démarrage, dernière écriture)`, sinon l'alerte s'allume à chaque lancement sur l'écriture de la veille.

### Pitfall 8 : semaine de forfait en `TimeSpan`
`WeeklyWindow.CurrentStart` et `WeeklyRecalibration.NextReset` avancent par `7 × 24 h` : dérive d'une heure à chaque changement
d'heure (vérifié par calcul : 2026-10-30T22:00Z contre 23:00Z). Les bornes de JRN-05 se calculent sur le calendrier local ; ces
deux classes ne sont pas à modifier dans cette phase (CAD-XX, v2) mais ne doivent pas être réutilisées pour les bornes.

### Pitfall 9 : le smoke `--hook` ne teste ni le mutex ni le journal
Ce mode sort avant le Host. Seul le constat (VAL-04) prouve CPT-03 et JRN-01 en vrai ; les tests unitaires prouvent la mécanique.

### Pitfall 10 : trois écrivains pendant le constat
Tant que 3.1.0 et 3.2.0 tournent, `treated.json`/`last-exact.json` sont écrits par trois processus ; le journal, lui, ne l'est que par
la 3.2.2 (les anciennes n'en ont pas) — mais le point (a) reste préalable à toute mesure.

## Code Examples

### Dédup « max par champ » (CPT-01), pur
```csharp
// Services/DedupUsage.cs — POURQUOI le max et non la première ligne : depuis Claude Code 2.1.260, chaque bloc de
// contenu est réécrit avec un output_tokens PARTIEL croissant ; la dernière ligne porte le total (mesuré 12 835 / 12 835).
public sealed class DedupUsage
{
    private readonly Dictionary<string, (DateTimeOffset Ts, long In, long Out, long CacheW, long CacheR)> _parId = new(StringComparer.Ordinal);
    private readonly List<(DateTimeOffset Ts, long Tokens)> _sansId = new();   // jamais jeter un token connu

    public void Ajouter(string? messageId, string? requestId, DateTimeOffset ts, long i, long o, long cw, long cr)
    {
        var cle = messageId ?? requestId;
        if (cle is null) { _sansId.Add((ts, i + o + cw + cr)); return; }
        if (_parId.TryGetValue(cle, out var v))
            _parId[cle] = (v.Ts <= ts ? v.Ts : ts, Math.Max(v.In, i), Math.Max(v.Out, o), Math.Max(v.CacheW, cw), Math.Max(v.CacheR, cr));
        else _parId[cle] = (ts, i, o, cw, cr);
    }

    public IEnumerable<(DateTimeOffset Ts, long Tokens)> Entrees()
        => _parId.Values.Select(v => (v.Ts, v.In + v.Out + v.CacheW + v.CacheR)).Concat(_sansId);
}
```

### Append exclusif idempotent (JRN-03)
```csharp
// Services/Historique/JournalReleves.cs — motif EcritureEtatSession.EcrireAvecReprise : partage EXCLUSIF, reprises bornées.
// POURQUOI pas FileMode.Append : sous Windows il fait un Seek(End) à l'ouverture ; deux écrivains → une ligne perdue (mesuré 2026-09-27).
private const int EssaisMax = 60; private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(25);
private const int QueueRelue = 16 * 1024;

private bool AjouterSiNouveau(string chemin, string source, DateTimeOffset t, ReadOnlySpan<byte> ligneUtf8)
{
    for (var essai = 0; ; essai++)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
            using var fs = new FileStream(chemin, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (DernierTPourSource(fs, source, QueueRelue) is { } dernier && t <= dernier) return false;   // idempotence (t, source)
            fs.Seek(0, SeekOrigin.End);
            fs.Write(ligneUtf8);           // une seule écriture, < 4 Ko, terminée par '\n'
            fs.Flush(flushToDisk: false);
            return true;
        }
        catch (IOException) when (essai < EssaisMax) { Thread.Sleep(Pause); }   // verrou tenu par l'autre écrivain
    }
}
```

### Lecture tolérante d'une ligne (JRN-03)
```csharp
// Source : TranscriptActivityProvider.ReadAsync l. 69-83 (même discipline).
try
{
    using var doc = JsonDocument.Parse(ligne);
    var o = doc.RootElement;
    if (!o.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != SchemaVersion) { ignorees++; continue; }
    if (!o.TryGetProperty("t", out var te) || !DateTimeOffset.TryParse(te.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var t)) { ignorees++; continue; }
    if (o.TryGetProperty("ev", out var ev)) evenements.Add(LireEvenement(t, ev.GetString(), o));   // ev inconnu → conservé « non reconnu »
    else releves.Add(LireReleve(t, o));                                                             // champ absent → null, jamais 0
}
catch (JsonException) { ignorees++; }   // ligne tronquée ou corrompue : sautée, jamais fatale
```

### Mutex mono-instance (CPT-03)
```csharp
// Services/VerrouInstanceUnique.cs — docs : avec initiallyOwned=true, le thread possède le mutex SEULEMENT si createdNew ;
// AbandonedMutexException sur WaitOne = l'ancien propriétaire est mort sans libérer → on POSSÈDE désormais le mutex.
public static ResultatVerrou Acquerir(string nom)
{
    var m = new Mutex(initiallyOwned: true, nom, out var cree);
    if (cree) return new(Obtenu: true, m, Abandonne: false);
    try { return m.WaitOne(0) ? new(true, m, false) : new(false, Dispose(m), false); }
    catch (AbandonedMutexException) { return new(true, m, Abandonne: true); }
}
```

### Sonde hors arbre (VAL-04, extrait vérifié le 2026-09-27)
```powershell
# Preuve de vue réelle : les deux premières lignes.
L ("parent: " + (Get-CimInstance Win32_Process -Filter "ProcessId = $PID").ParentProcessId)      # attendu : WmiPrvSE
L ("APPDATA\Claude existe : " + (Test-Path (Join-Path $env:APPDATA 'Claude')))                     # attendu : False
Get-ChildItem (Join-Path $env:APPDATA 'Chronos\historique') | % { L ("{0} {1} {2:o}" -f $_.Name, $_.Length, $_.LastWriteTime) }
Get-CimInstance Win32_Process -Filter "Name like 'Chronos%'" | % { L ("{0} pid={1} parent={2} cree={3:o}" -f $_.Name,$_.ProcessId,$_.ParentProcessId,$_.CreationDate) }
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| « une ligne `assistant` par bloc recopie le même `usage` » | `output_tokens` partiel croissant par bloc, dernière ligne = total | Claude Code 2.1.260 (observé entre le 28/08 en 2.1.247 et le 26/09 en 2.1.281) | dédup = max par champ, pas première ligne |
| Lecture des fichiers Chronos depuis une session | Sonde WMI hors arbre / diagnostic de l'overlay | phase 29 (25/09) — confirmé ici | le « gel » était un artefact |
| `WeeklyWindow` / `NextReset` en 7 × 24 h | bornes calendaires locales pour l'historique | cette phase (JRN-05) | dérive d'une heure au 25/10 rendue mesurable |

**Deprecated/outdated :** l'énoncé « `Save` dans un `try/catch` muet = cause du gel » (CONTEXT, STATE, conseil) — à réécrire dans
STATE.md par le SUMMARY de 32-02.

## Open Questions

1. **Pourquoi un overlay a-t-il tourné sous l'arbre de l'app le 13/09 (07:36 → 12:44) ?** Ce qu'on sait : copies COW de
   `settings.json`/`archived.json` à 07:36:58 (purge au démarrage) et `last-exact.json` à 12:44:59. Ce qui manque : le lanceur (terminal
   dans l'app ? agent ?). Recommandation : sans importance pour le code ; noter dans `data-sources.md` que « lancer l'exe depuis un
   terminal de l'app produit un second jeu de fichiers ».
2. **Fork/resume : 491 ids dans plusieurs fichiers.** Faut-il, en phase 33, dédoublonner par fichier (curseurs) ou globalement ?
   Recommandation pour CETTE phase : global pour `TranscriptActivityProvider` ; consigner le chiffre pour la phase 33.
3. **Granularité `utilization` (0,01) et Δ = consommation** — hypothèses à vérifier AVEC le journal, comme le veut le CONTEXT. Le
   journal écrit le `double` brut pour ne pas préjuger.
4. **`Process.StartTime` sur un processus d'un autre utilisateur** peut lever `Win32Exception` : traité par `try/catch`, âge « inconnu ».
5. **Forme exacte d'`overage`** : nombre seul (fraction) ou objet `{u, r, statut}` ? Recommandation : `overage` = fraction ou `null`,
   plus `overage_statut` (chaîne d'enum) optionnel ; le lecteur tolère les deux absences.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/test/publish | ✓ | 10.0.201 (cible net8.0-windows ; runtime de test 8.0.25) | — |
| xunit + Xunit.StaFact | tests | ✓ | 2.9.2 / 1.1.11 | — |
| Windows PowerShell 5.1 | sonde WMI | ✓ | (pwsh 7 absent) | — |
| WMI `Win32_Process.Create` | sonde hors arbre | ✓ (ReturnValue 0 le 27/09 01:56) | — | — |
| python3 | scans de transcripts (recherche seulement) | ✓ | — | — |
| Fuseau `Romance Standard Time` / `Europe/Paris` | tests DST | ✓ / ✓ (`HasIanaId`) | — | ids en dur dans les tests |
| Overlays en marche | constat | 3 (3.1.0 / 3.2.0 / 3.2.1, parent explorer) | — | geste utilisateur « Quitter Chronos » ×3 |
| `%APPDATA%\Chronos\historique` | journal | ✗ (créé par la 3.2.2 au premier relevé) | — | `Directory.CreateDirectory` à chaque écriture |

**Missing dependencies with no fallback :** aucune.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 + Xunit.StaFact 1.1.11 (Microsoft.NET.Test.Sdk 17.11.1), net8.0-windows |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemins `CheminSourcesChronos`, `CheminDocsChronos` injectés par MSBuild) |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<Classe>"` |
| Full suite command | `dotnet test Chronos.sln -c Debug --nologo -v q` (**1200 / 0 en 10 s** mesuré le 2026-09-27 avant la phase) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command (filtre) | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CPT-01 | trois lignes d'un même id → un seul `usage`, max par champ ; repli `requestId` ; sans id → compté | unit (pur) | `~DedupUsageTests` | ❌ Wave 0 |
| CPT-01 | `TranscriptActivityProvider.ReadAsync` sur la fixture réelle multi-blocs → `Since(T).Tokens` = un message | unit (E/S temp) | `~TranscriptActivityProviderTests` (+ fixture `TestData/transcript-multi-blocs.jsonl`) | ✅ classe, ❌ fixture |
| CPT-01 | `TokensDepuisReleve` hérite (doctrine + provider réel, fixture multi-blocs) | integration | `~LastExactUsageProviderTests` | ✅ classe, ❌ cas |
| CPT-01 | garde : aucune occurrence de `"usage"`/`"output_tokens"` hors du helper sous `Services/**`, `Models/**` ; ≥ 40 fichiers vus | garde textuelle | `~GardesJournalTests` | ❌ Wave 0 |
| CPT-02 | magasin en panne → `DerniereErreur`, événement `ecriture_ratee`, snapshot inchangé, `UnExactADejaEteObtenu == null` | unit | `~LastExactUsageProviderTests`, `~LastExactStoreTests` | ✅ classes, ❌ cas |
| CPT-02 | diagnostic : section `[Magasins persistants]`, « dernière écriture il y a N min », « Vue AppData : … » | unit (rapport texte) | `~DiagnosticServiceTests` | ✅ classe, ❌ cas |
| CPT-03 | second acquéreur (autre thread) refusé ; libération ; abandon = acquis | unit | `~VerrouInstanceUniqueTests` | ❌ Wave 0 |
| CPT-03 | « N processus Chronos » : filtre par préfixe, exclusion du PID courant, âge (`InventaireProcessus` sur une liste injectée) | unit (pur) | `~InventaireProcessusTests` | ❌ Wave 0 |
| CPT-03 | `CompositionRootTests` : le journal est inscrit ENTRE la tête et le composite ; hosted service avant l'orchestrateur | garde DI | `~CompositionRootTests` | ✅ classe, ❌ cas |
| JRN-01 | 6 `GetAsync` avec la même instance → 1 ligne ; plancher/Unavailable/`MagasinDernierExact` → 0 ligne ; deux sources → deux lignes | unit | `~JournalisationUsageProviderTests` | ❌ Wave 0 |
| JRN-02 | `demarrage`/`arret` (Start/Stop), `jeton_invalide` (FakeAuthStatus.Declencher), `sonde_refusee` (FakeEtatServeur.DernierResultat, transition seule), `reprise` (> 10 min) | unit | `~JournalisationUsageProviderTests` | ❌ Wave 0 |
| JRN-03 | deux écrivains, 200 écritures → 0 doublon, 0 tronquée ; ligne tronquée / `v` inconnu / `ev` inconnu ignorés ; mois UTC ; rétention 24 mois ; aucun type WPF | unit + concurrence + garde | `~JournalRelevesTests`, `~ServicesLayerPurityTests` | ❌ / ✅ (à élargir) |
| JRN-04 | `FakeClock` : 16 min sans écriture → alerte ; démarrage récent → pas d'alerte ; texte « journal muet depuis N min » | unit VM (STA si binding) | `~MainViewModelTests`, `~ReglagesBindingTests` | ✅ classes, ❌ cas |
| JRN-05 | série, trous et causes, resets, Δ de même `r`, saut non localisé, bornes semaine/jour/4 semaines, DST 25/10/2026 et 28/03/2027, fichier à cheval sur deux mois | unit (pur + E/S temp) | `~AnalyseRelevesTests`, `~LecteurJournalTests` | ❌ Wave 0 |
| JRN-06 | quatre propriétés cohérentes ; doc §7 nomme chaque champ/événement réel | garde | `~VersionPublieeTests`, `~ContratJournalDocumenteTests` | ✅ / ❌ Wave 0 |
| VAL-04 | constat humain | manual-only (l'agent ne lance pas l'overlay) | sonde WMI + `32-CONSTAT.md` | — |

### Sampling Rate
- **Per task commit :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<classe du plan>"`
- **Per wave merge :** `dotnet test Chronos.sln -c Debug --nologo -v q` (10 s)
- **Phase gate :** suite complète verte deux fois de suite avant `/gsd:verify-work` ; 1200 + nouveaux, 0 échec.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/TestData/transcript-multi-blocs.jsonl` — fixture réelle anonymisée (3 lignes même id, `output_tokens` 8/8/256, + 1 doublon strict) — CPT-01
- [ ] `tests/Chronos.Tests/TestData/journal/*.jsonl` — 7 fixtures de journal (voir JRN-05) — JRN-03, JRN-05
- [ ] `tests/Chronos.Tests/Fakes/FakeJournalReleves.cs` (ou interface `IJournalReleves` + faux) — CPT-02, JRN-04
- [ ] Élargissement de `ServicesLayerPurityTests` (namespace `StartsWith`), `GardesDoctrineTests` ×2 et `NormalisationUniqueTests` (`AllDirectories`) + test « la garde voit `Chronos.Services.Historique` » — dans 32-04
- [ ] `DedupUsageTests`, `VerrouInstanceUniqueTests`, `InventaireProcessusTests`, `JournalRelevesTests`, `JournalisationUsageProviderTests`, `LecteurJournalTests`, `AnalyseRelevesTests`, `GardesJournalTests`, `ContratJournalDocumenteTests` — nouvelles classes
- Framework : rien à installer.

## Sources

### Primary (HIGH confidence)
- Code du dépôt lu ce jour : `App.xaml.cs` (l. 15-126, 211-441), `Services/LastExactStore.cs`, `LastExactUsageProvider.cs`,
  `TranscriptActivityProvider.cs`, `ITranscriptActivitySource.cs`, `DoctrineFraicheur.cs`, `RateLimitHeaderUsageProvider.cs`
  (l. 196-260, 355-445, 480-530), `RefreshOrchestrator.cs`, `CompositeUsageProvider.cs`, `ChronosTokenAuthority.cs`, `IEtatServeur.cs`,
  `IAuthStatus.cs`, `ResultatSonde.cs`, `DiagnosticService.cs`, `EcritureEtatSession.cs`, `ChronosPaths.cs`, `ChronosSettings.cs`,
  `WeeklyWindow.cs`, `WeeklyRecalibration.cs`, `RacinesEtat.cs`, `Text/LibelleSource.cs`, `ViewModels/MainViewModel.cs` (l. 238-330,
  403-417), `Views/SettingsWindow.xaml` (l. 156-206), `Resources/DesignTokens.xaml`, `Chronos.csproj`, tests `ServicesLayerPurityTests`,
  `GardesDoctrineTests`, `GardesPerimetreTests`, `ContratHooksDocumenteTests`, `LastExactStoreTests`, `LastExactUsageProviderTests`,
  `TranscriptActivityProviderTests`, `DeuxVuesAppDataTests`, `Fakes/*`.
- **Sonde WMI hors arbre du 2026-09-27 01:56:15** (parent `WmiPrvSE`, `%APPDATA%\Claude` = False) : listing réel de `%APPDATA%\Chronos`,
  contenu de `last-exact.json`, processus `Chronos*`, ACL ; fichiers de sonde supprimés après lecture.
- Listing direct de `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Chronos` (copies COW datées du 13/09 et 14/09).
- Scans Python lecture seule de `~/.claude/projects` (8 derniers jours : 934 fichiers, 1,28 Go ; 400 plus récents pour la
  divergence d'`usage`) et du plus gros transcript (3 248 lignes / 1 183 ids).
- Essai .NET 8.0.25 (console jetable dans le bloc-notes de session) : `FileMode.Append` concurrent, `TimeZoneInfo` IANA/DST,
  `Process.GetProcesses()` sans admin, dérive de `NextReset`.
- `dotnet test Chronos.sln -c Debug --nologo -v q` → 1200 / 0, 10 s.
- Microsoft Learn — `Mutex(Boolean, String, Boolean)` : propriété seulement si `createdNew` ; `WaitOne` sinon —
  https://learn.microsoft.com/en-us/dotnet/api/system.threading.mutex.-ctor?view=net-8.0
- Microsoft Learn — `FileMode.Append` : « Opens the file if it exists and seeks to the end of the file » —
  https://learn.microsoft.com/en-us/dotnet/api/system.io.filemode?view=net-8.0
- Microsoft Learn — `File.Move(String, String, Boolean)` : `UnauthorizedAccessException` « failed to acquire an exclusive access to the
  destination file » — https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-8.0
- `.planning/phases/29-…/29-SONDE-HORS-ARBRE.txt` (25/09 21:17 : `last-exact.json 25.09 21:16` en vue réelle), `31-CONSTAT.md`,
  `31-02-SUMMARY.md`, `31-03-PLAN.md`, `docs/publish.md`, `docs/data-sources.md`, `docs/desktop-app-sessions.md` §7, mémoire
  `chronos-app-bureau-metadonnees-sessions.md`.

### Secondary (MEDIUM confidence)
- Volumétrie du journal (≈ 230 o/ligne → ≈ 2 Mo/mois) : calculée sur le format proposé, non mesurée en production.
- Comportement de `AbandonedMutexException` à l'acquisition : documenté (classe `Mutex`, remarques) mais non rejoué ici ; à couvrir par
  `VerrouInstanceUniqueTests`.

### Tertiary (LOW confidence)
- Cause de la présence d'un overlay virtualisé le 13/09 (terminal dans l'app ?) — déduite des mtimes, non prouvée.

## Metadata

**Confidence breakdown :**
- Faits sur le « gel » : HIGH — deux sondes hors arbre concordantes (25/09, 27/09) + copies COW listées.
- Règle de dédup : HIGH — 12 835 / 12 835 cas monotones, dernière ligne = max ; 0 ligne sans `message.id`.
- Atomicité d'append : HIGH — mesuré sur le runtime cible.
- Architecture (décorateur, namespaces, gardes) : MEDIUM-HIGH — déduite du code lu, choix de forme à discrétion.
- Fuseaux/DST : HIGH — calculé sur la machine cible avec les deux identifiants de fuseau.

**Research date :** 2026-09-27
**Valid until :** 2026-10-27 (30 j) — sauf changement de version de Claude Code (schéma `usage`) ou de l'app bureau (virtualisation).
