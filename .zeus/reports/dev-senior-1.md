# Rapport dev-senior — Chronos (release 3.5.0)

> Phases 2 (diagnostic) et 3 (plan de refactoring). C'est une **proposition pure** : rien n'a été appliqué.
> État lu : commit `aeb6f0f`, le 2026-10-04. Ce rapport s'appuie sur la cartographie validée,
> `.zeus/reports/dev-senior/ARCHITECTURE.md`. Je n'ai pris aucune source dans les rapports internes (`.zeus/`, `.planning/`).
> Je n'ai lancé ni l'exe ni les tests. Chemins relatifs à `src/Chronos/`, sauf mention contraire.
> Outils utilisés :
> - `dotnet build` sur une copie dans le scratchpad : 0 avertissement ;
> - `dotnet test --list-tests` : 2 152 cas ;
> - mesures en lecture seule sur la machine réelle (métadonnées des transcripts seulement, jamais leur contenu) ;
> - grep.

## Synthèse

**État général** : le code est solide, défensif et très testé (2 152 cas, build sans avertissement). L'essentiel de la
doctrine « exact ou rien » est réellement appliqué. Trois défauts bloquent pourtant une publication 3.5.0 en l'état :

**Les 3 risques principaux** (les trois bloquants)
1. **DS-ARCH-01** : la déconnexion et le changement de compte depuis les Réglages ne sont pas pris en compte par
   l'autorité de jeton. Au rafraîchissement suivant, `oauth.dat` est réécrit avec les anciens jetons. Les jetons d'un
   nouveau login sont alors **écrasés en silence**.
2. **DS-ARCH-02** : l'état « Exact — encore valide » affiche un pourcentage **sans signe**. Il est certifié par les
   seuls transcripts Claude Code, qui, de l'aveu même du code et de la doc, ne voient ni l'app bureau, ni Cowork, ni
   claude.ai. Or ces trois consommateurs partagent le même pool.
3. **DS-MAINT-01** : la version embarquée est toujours **3.4.0**. La convergence de l'autostart refuse de repointer vers
   un exe de version égale : au redémarrage suivant, c'est l'**ancien exe** qui se relancerait.

**Les 3 forces**, avec preuve
- Les écritures sur `~/.claude/settings.json` passent par une passerelle unique. Elle sauvegarde avant d'écrire
  (`PasserelleReglagesClaude.cs:143-149`), refuse un fichier illisible (l. 131-132) ou un lien symbolique (l. 138-139),
  vérifie que le fichier n'a pas changé entre lecture et écriture (l. 158-162), puis remplace le fichier de façon atomique.
- Une absence ne produit jamais d'affirmation : champs `bool?` et `null`, enums sans membre fourre-tout
  (`UsageSnapshot.cs:28`, `SourceUsage.cs:19-29`), état « Indisponible » plutôt qu'une valeur inventée
  (`DoctrineFraicheur.cs:65-80`).
- L'arrêt est borné (`ArretHote.cs:28-43`), l'instance est unique grâce à un mutex (`App.xaml.cs:93-102`), trois
  filets globaux couvrent les exceptions (`App.xaml.cs:217-234`) et le journal d'incidents est plafonné
  (`LimiteurIncidents.cs:50-87`).

**Décompte des 23 findings** : **3 Critiques**, **8 Majeurs**, **10 Mineurs**, **2 Hypothèses à vérifier**.

---

## Verdicts AUDIT_POINTS

| # | Point | Verdict | Motif principal |
|---|---|---|---|
| 1 | Honnêteté des chiffres | **BLOQUANT** | DS-ARCH-02 (« encore valide » affiché comme exact sur une prémisse que le projet sait fausse pour Cowork, l'app bureau et claude.ai). Écart à arbitrer en plus : DS-ARCH-05 (resets 5 h extrapolés dessinés comme des resets). |
| 2 | Intégrité des données | **BLOQUANT** | DS-ARCH-01 (jetons d'un nouveau login écrasés en silence ; déconnexion annulée en silence). |
| 3 | Robustesse de l'overlay | **Écarts mineurs** (non bloquants) | E/S disque sur le thread UI toutes les 2 s (DS-PERF-02) et au démarrage (DS-PERF-03) ; appels concurrents à la chaîne (DS-ARCH-03, DS-ARCH-04). Aucun plantage ni zombie atteignable relevé. |
| 4 | Reprenabilité | **Écarts mineurs** | Objets dieux sur les points chauds (DS-MAINT-02), commentaires périmés (DS-MAINT-03), reliquats (DS-MAINT-04), 1 120 références à des identifiants internes non résolubles (DS-MAINT-05). |
| 5 | Prêt à distribuer | **BLOQUANT** | DS-MAINT-01 (csproj en 3.4.0 ; l'autostart garde l'ancien exe). Écarts mineurs en plus : DS-MAINT-06 (pas de `global.json`, pas de CI), `docs/publish.md` §5 périmé (DS-MAINT-03). |

### Point 1 — Honnêteté des chiffres : **BLOQUANT**

**Conforme**
- **Le plancher est le seul à porter « ≥ »** : `PercentFormatter.cs:47` ; doctrine `DoctrineFraicheur.cs:85-92`.
- **Les tokens ne sont jamais convertis en pourcentage** :
  - au cadran, `TokensText` reste séparé (`WindowGaugeViewModel.cs:141-142`) ;
  - dans l'Historique, `Divergences.Detecter` compare des deltas de pourcentage à la *présence* de tokens, sans conversion (`Historique/Divergences.cs:42-59`) ;
  - les agrégats restent sur leur propre axe (`docs/data-sources.md:56-62`, vérifié contre `RenduLocalTokens`).
- **Aucun trou n'est interpolé** : grep `Interpol|Lerp` vide dans `Rendering/Historique`, `Controls/Historique` et `Services/Historique`.
- **Les chiffres utilisent les resets du serveur** : `resets_at` lu dans les en-têtes ou en JSON (`RateLimitHeaderUsageProvider.cs:403`, `ChronosOAuthUsageProvider.cs:178`), persisté tel quel (`LastExactStore.cs:209-213`), et une fenêtre est écartée dès que son reset est passé (l. 222).
- **`WeeklyAnchor` n'est jamais écrit** : vérifié et **conforme**. La doc le déclare lu en secours par l'Historique seulement (`docs/data-sources.md:117-118`), et rien ne l'introduit dans le cadran.

**Bloquant** : DS-ARCH-02. **À arbitrer** : DS-ARCH-05 (voir les findings).

### Point 2 — Intégrité des données : **BLOQUANT**

**Conforme**
- **Réglages** : lecture tolérante, et un fichier illisible part en quarantaine avec un incident journalisé avant toute réécriture (`SettingsService.cs:269-318`). Une lecture inaccessible fait refuser l'écriture, avec incident (l. 274-277, 389-399).
- **Archives et traitées** : quarantaine puis repartir du dernier contenu connu (`MagasinMapSessions.cs:147-167`), écriture atomique sous un nom temporaire unique (l. 179-185).
- **Journal des relevés** : idempotence relue sous le même verrou exclusif (`JournalReleves.cs:136-146, 219-248`).
- **Agrégats** : l'ordre de flush est ids → agrégats → ids gelés → curseurs, et un mois illisible interrompt la passe sans écrire (`ReconstructionTokens.cs:428-467, 396-402`).
- **`~/.claude/settings.json`** : voir les forces.

**Bloquant** : DS-ARCH-01.

**Écarts mineurs**
- Les commentaires d'un `settings.json` JSONC sont perdus à la réécriture : la lecture les saute (`ClaudeSettingsJson.cs:51-55`) et la sérialisation les supprime (l. 156). Une sauvegarde est faite avant, 5 sont conservées plus une épingle.
- La fusion de `last-exact.json` peut perdre la mise à jour d'une fenêtre en cas d'appels concurrents (DS-ARCH-03). L'effet est transitoire : le tick suivant répare.

### Point 3 — Robustesse de l'overlay : **Écarts mineurs**

**Conforme**
- Instance unique (`VerrouInstanceUnique.cs:59-73`, `App.xaml.cs:93-102`).
- Arrêt borné hors du thread UI, avec sortie forcée en dernier recours (`App.xaml.cs:291-309`, `ArretHote.cs`). Aucun chemin vers un processus zombie relevé.
- Filets globaux et démarrage protégé (`App.xaml.cs:85, 108-196`).
- Incidents journalisés et plafonnés (`JournalIncidents.cs:61-100`).
- Mode `--hook` : code de sortie honnête, jamais 2 (`App.xaml.cs:246-273`).

**Écarts** : DS-PERF-02, DS-PERF-03, DS-ARCH-03, DS-ARCH-04. Ce sont des Majeurs, à traiter après la release. Aucun ne
produit de plantage : les exceptions de `Observe` sont avalées (`SessionMonitor.cs:210`).

### Point 4 — Reprenabilité : **Écarts mineurs**

`docs/data-sources.md` §1 décrit fidèlement la chaîne réelle (vérifié contre `App.xaml.cs:502-540`). En revanche :
- `MainViewModel`, `DiagnosticService` et `App.xaml.cs` sont à la fois les plus gros fichiers et les plus modifiés (DS-MAINT-02) ;
- plusieurs commentaires et une doc contredisent le code (DS-MAINT-03) ;
- il reste quatre reliquats de méthodes abandonnées (DS-MAINT-04), alors que le point d'audit dit « pas de reliquat » ;
- 31 % des lignes sont des commentaires, avec 1 120 références à des identifiants de planification (DS-MAINT-05).

### Point 5 — Prêt à distribuer : **BLOQUANT**

**Bloquant** : DS-MAINT-01.

**Conforme**
- Exe autonome : profil `win-x64.pubxml` self-contained, mono-fichier, sans trim.
- Les propriétés de publication sont conditionnées (`Chronos.csproj:28-37`).
- Cohérence des 4 propriétés de version gardée par `tests/Chronos.Tests/VersionPublieeTests.cs:20-41`.
- Hooks repointés au démarrage (`ClaudeSettingsReconciler.cs:252-296`).

**Écarts mineurs**
- `docs/publish.md` §5 dit que le raccourci « reste valide tant que l'exe n'est pas déplacé » et qu'il faut le re-basculer. Le code le repointe pourtant automatiquement (`AutostartService.cs:115-138`).
- `README.md:25` demande de télécharger `Chronos.exe`, alors que `docs/publish.md:42` nomme l'exe `Chronos-v<X.Y.Z>.exe`.
- `README.md:206` annonce « plus de 1 600 tests » (2 152 cas aujourd'hui).
- DS-MAINT-06 : pas de `global.json`, de CI ni de lockfile.

---

## Findings par axe

### Axe 1 — Architecture

#### [DS-ARCH-01] La déconnexion et le changement de compte ne réinitialisent pas l'autorité de jeton
- **Axe** : architecture
- **Sévérité** : **Critique** (bloquant, point 2)
- **Preuve**
  - `MainViewModel.cs:843-850` : `LoginClaude` → `Logout()` ou `LoginAsync()`, puis `RequestRefresh()`, **sans** `ReinitialiserApresLogin()`. Seul `ReconnecterAsync` l'appelle (l. 868).
  - `Views/OAuthLogin.cs:28` : `Logout() => _store.Clear()`, qui ne supprime que le fichier.
  - `ChronosTokenAuthority.cs:44` et `:76` : copie mémoire `_jetons ??= _coffre.Load()`, jamais relue tant qu'elle n'est pas nulle.
  - `ChronosTokenAuthority.cs:89-114` : au rafraîchissement, `_coffre.Save(_jetons)` (l. 109) **recrée** `oauth.dat`.
  - `TokenRefreshService.cs:53, 63-68` : tick toutes les 60 s.
  - Aucun test ne couvre « l'autorité oublie ses jetons après déconnexion ». `MainViewModelTests.cs:643-657` vérifie seulement `LogoutCount == 1` et `IsLoggedIn == false`, avec une `FakeAuthStatus`.
- **Constat**
  - Après « Se déconnecter », l'autorité continue de servir l'ancien jeton. Les chiffres continuent de s'afficher et la pastille reste « Connecté ».
  - Dès que le jeton approche de l'échéance (marge de 12 min), le rafraîchissement réécrit `oauth.dat`.
  - Après un nouveau login via la même commande, les nouveaux jetons sont sauvegardés, mais l'autorité garde les anciens. Au rafraîchissement suivant, les jetons de l'ancien compte **écrasent** ceux du nouveau login.
- **Pourquoi c'est un problème** : un geste explicite de l'utilisateur est annulé en silence et un coffre persistant de Chronos est corrompu. Pendant ce temps, l'overlay affiche des chiffres « exacts » d'un compte autre que celui que l'utilisateur croit connecté.
- **Correction proposée** : voir P-01. C'est un changement de comportement voulu, donc une correction et non un refactoring iso-fonctionnel.

#### [DS-ARCH-02] « Exact — encore valide » certifié par une source qui ne voit pas tout le pool
- **Axe** : architecture (doctrine)
- **Sévérité** : **Critique** (bloquant, point 1)
- **Preuve**
  - `DoctrineFraicheur.cs:82-86` : si le journal ne montre aucune activité depuis la capture, la fenêtre reste `Exact`, avec la provenance `EncoreValide`.
  - `PercentFormatter.cs:47` : seul `PlancherAvecActivite` reçoit « ≥ ». `EncoreValide` s'affiche **sans signe**.
  - `docs/data-sources.md:108-109` : « Exact — encore valide … le pourcentage, sans signe ».
  - **Contradiction écrite dans le projet lui-même** :
    - `docs/data-sources.md:56-57` : « les transcripts ignorent l'app bureau et Cowork, qui consomment le même pool » ;
    - `Models/WindowState.cs:50-52` dit la même chose ;
    - `docs/data-sources.md:288-290` ajoute claude.ai.
  - Aggravant : `SourceActiviteMemoisee.cs:46-58`. Si la relecture échoue, l'**ancien** journal est rendu quel que soit son âge (`catch when (_journal is not null)`). `Statuer` ne compare jamais `journal.Now` à `now` (`DoctrineFraicheur.cs:80-86`). L'activité survenue après l'ancien `Now` est donc invisible, et un « encore valide » peut être prononcé sur un journal périmé.
- **Constat**
  - Dès que les sources vivantes échouent plus de 360 s (jeton `Deconnecte` ou `HorsLigne`, 429 sans en-têtes, sonde désactivée avec un OAuth en échec), le dernier relevé reste affiché comme exact.
  - Cela vaut jusqu'à son reset (jusqu'à 7 jours pour la fenêtre hebdo), tant que Claude Code n'écrit rien.
  - L'usage via Cowork, l'app bureau ou claude.ai fait monter le vrai chiffre sans que Chronos le voie.
  - Une pastille « relevé daté » s'affiche (`MainWindow.xaml:134-138`), mais le nombre lui-même ne porte aucune marque d'incertitude.
- **Pourquoi c'est un problème** : c'est exactement le cas « estimation présentée comme exacte » que le point 1 interdit. Cowork fait partie du public visé (CLAUDE.md : « pour Claude Code et Cowork »).
- **Correction proposée** : voir P-02. C'est une suggestion produit, à trancher par l'utilisateur.
  - Ma recommandation : un relevé vieilli est par nature une **borne inférieure** (dans une fenêtre, l'utilisation ne peut que croître jusqu'au reset). Afficher « ≥ » pour `EncoreValide` est donc exact, et aligné sur la doctrine.
  - Il faut aussi corriger le retour du journal périmé.
  - Si l'utilisateur accepte l'hypothèse « Claude Code est le seul consommateur », elle doit être **écrite** dans AUDIT_POINTS.md (point 1) et dans `docs/data-sources.md` §4, où elle n'apparaît pas aujourd'hui.

#### [DS-ARCH-03] Le diagnostic appelle la tête de chaîne hors de l'orchestrateur
- **Axe** : architecture
- **Sévérité** : **Majeur**
- **Preuve**
  - Invariant déclaré : « consommateur UNIQUE : sérialise les GetAsync » (`RefreshOrchestrator.cs:10-11, 50`).
  - Pourtant :
    - `App.xaml.cs:558` injecte la tête `IUsageProvider` sous le nom `composite` ;
    - `DiagnosticService.cs:155` appelle `_composite.GetAsync(ct)` ;
    - appelants : `App.xaml.cs:175` (fire-and-forget au démarrage, alors que la charge initiale de l'orchestrateur est encore en vol, lancée à `App.xaml.cs:141`) et `MainViewModel.cs:470` (`Task.Run`).
  - État mutable non verrouillé dans les sources : `RateLimitHeaderUsageProvider.cs:165-168` et `ChronosOAuthUsageProvider.cs:50-52`.
  - `LastExactStore.cs:129` : nom temporaire **par processus** (`.tmp-{ProcessId}`), partagé par les deux threads du même processus, et fusion lire-modifier-écrire sans verrou (l. 111-131).
- **Constat** : à chaque démarrage, deux appels de la chaîne peuvent se chevaucher.
  - Les deux peuvent franchir le frein de la sonde avant que `_prochainAppelAutorise` ne soit posé : on obtient alors une double micro-requête.
  - Les deux `Save` se disputent le même fichier temporaire. L'un lève, ce qui déclenche l'événement `EcritureRatee` et donc une fausse ligne `ecriture_ratee` dans le journal (`App.xaml.cs:534`). Ou bien l'un écrase la fusion de l'autre.
  - Le diagnostic écrit aussi dans `last-exact.json` et dans le journal : ce n'est pas un observateur passif.
- **Pourquoi c'est un problème** : la seule garantie de sérialisation de la chaîne est contournée par construction. Le prochain état ajouté à un provider héritera de cette concurrence sans le savoir.
- **Refactoring proposé** : voir P-03. Le diagnostic lit le dernier snapshot publié par l'orchestrateur, et le chemin de démarrage passe par `Task.Run`.

#### [DS-ARCH-04] Une lecture qui écrit, sans verrou : `SessionTreatmentTracker`
- **Axe** : architecture
- **Sévérité** : **Majeur**
- **Preuve**
  - `SessionMonitor.cs:210` : `Inspecter` appelle `_tracker?.Observe(...)`.
  - `SessionTreatmentTracker.cs:116-182` : `Observe` mute trois `Dictionary` (l. 39-43) et écrit `TreatedStore.Set` / `Remove` (l. 134, 170, 181). Il n'a **aucun** `lock` (grep vide), alors que `LecteurAppBureau.cs:198` et `PremierPlanWin32.cs:108` en ont un.
  - Deux appelants concurrents : le timer UI de 2 s (`SessionsViewModel.cs:185-186` → `Refresh` → `_monitor.Read`) et `DiagnosticService.cs:469` (`Inspecter`, sur le pool au démarrage et depuis les Réglages).
- **Constat**
  - Une demande de diagnostic compte comme un cycle d'observation : elle peut faire passer une session à « répondue » ou « lue » et l'inscrire dans `treated.json`.
  - Deux `Observe` simultanés modifient les mêmes dictionnaires non thread-safe. Sous .NET 8, l'issue est une `InvalidOperationException`, avalée par `SessionMonitor.cs:210`, ou un état incohérent.
- **Pourquoi c'est un problème** : le masquage d'une session « en attente » peut dépendre de la date d'un diagnostic, et le défaut est intermittent, donc difficile à reproduire.
- **Refactoring proposé** : voir P-04.

#### [DS-ARCH-05] Resets 5 h extrapolés dessinés comme des resets (à arbitrer)
- **Axe** : architecture (doctrine)
- **Sévérité** : **Majeur**, non bloquant à mon avis, mais à trancher
- **Preuve**
  - `Rendering/DayTimeline.cs:15` : « les resets 5 h tombent toutes les 5 h » ; l. 29-53 : marques à `reset ± k·5 h` sur toute la journée.
  - Liées en mode **Normal** (le défaut) du style **Anneaux** (le défaut) : `Views/Cadrans/CadranArcsView.xaml:92, 130`, binding `DayResetAngles`.
  - `README.md:18` : « des marques à chaque reset 5 h ».
  - Point 1 : « resets toujours ceux du serveur » ; `docs/data-sources.md:117` : « Les resets viennent toujours du serveur ».
- **Constat**
  - La fenêtre 5 h est glissante : le serveur ne donne qu'**un** reset. Les autres marques sont des projections, dessinées dans le même style que le reset réel.
  - Le chiffre affiché n'est pas faussé, mais l'axe montre des resets qui n'existent pas.
- **Pourquoi c'est un problème** : cela contredit la lettre du point 1 et de la doc. Je ne le classe pas bloquant, parce que ce n'est pas un chiffre et que le reset réel est aussi dessiné.
- **Correction proposée** : voir P-05. C'est une suggestion produit.

#### [DS-ARCH-06] Cycles entre espaces de noms
- **Axe** : architecture
- **Sévérité** : **Mineur**
- **Preuve** : cycle `Services → Theming → Rendering → Services`, via `Services/ISessionsController.cs:1`, `Theming/ChronosTheme.cs:6` et `Rendering/EmpreinteCadran.cs:2, 11` (`CadranStyle`, défini dans `Services/ChronosSettings.cs:14`). Autres cycles :
  - `Services ↔ Text` : `DiagnosticService.cs:9`, `Text/TextesHistorique.cs:3` ;
  - `Services.Historique ↔ Tokens` : `Tokens/IndexMessages.cs:9`, pour `BilanRetention` défini dans `JournalReleves.cs:14`.
- **Constat** : des enums de préférence (`CadranStyle`, `OrientationCadran`) vivent dans Services, et un contrat de Services expose un type de thème WPF.
- **Pourquoi c'est un problème** : le code reste dans un seul assembly, donc l'impact est faible aujourd'hui. Mais ces cycles empêchent toute extraction d'un « cœur » testable sans WPF.
- **Refactoring proposé** : déplacer `CadranStyle`, `OrientationCadran`, `SessionStyle` et `CadranDisplayMode` dans `Models/`, puis `BilanRetention` dans `Models/Historique`. C'est iso-fonctionnel : seuls les namespaces changent.

### Axe 2 — Duplication

#### [DS-DUP-01] Extraction « message assistant, horodatage, usage » dupliquée, avec des règles déjà divergentes
- **Axe** : duplication (réelle : même règle métier)
- **Sévérité** : **Majeur**
- **Preuve**
  - **Prédicat `EstAssistant` identique** : `TranscriptActivityProvider.cs:135-144` et `Tokens/LecteurTranscript.cs:221-229`.
  - **Parsing de l'horodatage divergent** :
    - `TranscriptActivityProvider.cs:82-83` utilise `DateTimeOffset.TryParse` directement ;
    - `LecteurTranscript.cs:187-189` passe par `UsageNormalization.InstantDepuisIso`, qui ajoute un plancher à 2020 (`UsageNormalization.cs:125-128`) ;
    - la doc affirme pourtant que « toutes les conversions … passent par `UsageNormalization`, point unique » (`docs/data-sources.md:120`).
  - **Politique des horodatages futurs divergente** : ignorés en silence dans `TranscriptActivityProvider.cs:85`, mais tolérance et blocage dans `LecteurTranscript.cs:192-197`.
  - **Détection des sous-agents divergente** : `TranscriptSessionSource.cs:107-109` (dossier `subagents` **ou** préfixe `agent-`) contre `LecteurTranscript.cs:66-67` (dossier `subagents` seulement).
- **Constat** : trois lecteurs d'une même source, dont deux appliquent la même règle avec des variantes.
- **Pourquoi c'est un problème** : un changement de format des transcripts devra être corrigé à deux ou trois endroits. Le plancher affiché (`TokensDepuisReleve`) et les agrégats de l'Historique peuvent déjà compter différemment les mêmes lignes.
- **Refactoring proposé** : voir P-06. Ne **pas** fusionner `TranscriptSessionSource` : il classe un état (dernier message, `tool_use`), pas des tokens. C'est une similarité accidentelle.

#### [DS-DUP-02] Algorithme de quarantaine écrit deux fois
- **Axe** : duplication
- **Sévérité** : **Mineur**
- **Preuve** : `SettingsService.cs:285-318` (`MettreEnQuarantaine`) et `QuarantaineFichier.cs:28-68` (`Mettre`) ont le même schéma de nom `*.illisible-yyyyMMdd-HHmmss[-n]`, les mêmes 100 essais de nom et la même reprise sur collision. `MagasinMapSessions.cs:156` réutilise `QuarantaineFichier`, mais pas `SettingsService`.
- **Constat** : deux implémentations d'une même règle. Seule celle de `QuarantaineFichier` gère les reprises sur un fichier verrouillé (l. 54-57).
- **Refactoring proposé** : faire appeler `QuarantaineFichier.Mettre(_paths.SettingsFile, out cible, out cause)` par `SettingsService` et garder sa journalisation. C'est iso-fonctionnel, au gain près des reprises sur verrou.

#### [DS-DUP-03] Écriture atomique « tmp puis Move » répétée 7 fois, avec un nommage du temporaire divergent
- **Axe** : duplication
- **Sévérité** : **Mineur**
- **Preuve**
  - Nom par processus : `LastExactStore.cs:129`, `Tokens/CouvertureTokens.cs:173`, `Tokens/Curseurs.cs:138`, `Tokens/MagasinAgregats.cs:235`.
  - Nom unique (`ProcessId` + GUID) : `SettingsService.cs:359`, `MagasinMapSessions.cs:179`, `PasserelleReglagesClaude.cs:153`.
- **Constat** : la variante « par processus » suppose un seul écrivain par fichier dans le processus. Cette hypothèse est fausse pour `last-exact.json` (DS-ARCH-03).
- **Refactoring proposé** : un helper `EcritureAtomique.Ecrire(chemin, octets)` avec un nom unique, utilisé partout. C'est iso-fonctionnel.

#### [DS-DUP-04] Classification des sous-agents différente selon le lecteur
- **Axe** : duplication
- **Sévérité** : **Hypothèse à vérifier**
- **Preuve** : `TranscriptSessionSource.cs:107-109` contre `LecteurTranscript.cs:66-67`.
- **Constat** : un fichier `agent-*.jsonl` hors d'un dossier `subagents` est traité comme un sous-agent par le widget, mais comme un agent principal par les agrégats. Je n'ai pas vérifié si ce format existe encore dans les transcripts actuels (je n'ai pas lu le contenu des transcripts).
- **Refactoring proposé** : une seule règle, `Transcripts.EstSousAgent(chemin)`, après avoir confirmé le format réel.

### Axe 3 — Performance (sans profiling exécuté : sévérité plafonnée)

#### [DS-PERF-01] Passe intégrale des transcripts à chaque tick en mode dégradé
- **Axe** : performance
- **Sévérité** : **Majeur** (coût mécanique certain ; impact à profiler)
- **Preuve**
  - Ce mode se déclenche dès qu'un candidat a plus de 360 s (`LastExactUsageProvider.cs:94-100` ; `DoctrineFraicheur.cs:50-54`), donc à chaque tick tant que les sources vivantes échouent.
  - La mémoïsation dure 60 s (`SourceActiviteMemoisee.cs:21`), soit la cadence du tick (`App.xaml.cs:548`) : il n'y a aucune réutilisation d'un tick à l'autre.
  - La passe relit tous les `*.jsonl` de moins de 8 jours, en entier (`TranscriptActivityProvider.cs:55-101, 117-119`).
  - **Mesure sur la machine réelle** : 727 fichiers de moins de 8 jours, **882 Mo**. Le commentaire de `App.xaml.cs:422` annonçait 536 Mo et 2,7 à 3,2 s par passe.
- **Constat** : hors ligne, jeton invalide ou sonde en 429 sans en-têtes, Chronos relit environ 880 Mo par minute, et ce volume croît avec l'usage.
- **Refactoring proposé** : voir P-07.

#### [DS-PERF-02] Scan du widget de sessions sur le thread UI toutes les 2 s
- **Axe** : performance
- **Sévérité** : **Majeur** (point 3 : « thread UI jamais bloqué »)
- **Preuve**
  - `SessionsViewModel.cs:185-186` : un `DispatcherTimer` de 2 s appelle `Refresh`, qui appelle `_monitor.Read(now)` **de façon synchrone sur le thread UI** (l. 195).
  - `SessionMonitor.Inspecter` (`SessionMonitor.cs:119-252`) enchaîne :
    - l'énumération récursive de **tous** les `*.jsonl` (`TranscriptSessionSource.cs:79-85`) ;
    - la lecture de la queue de 64 Ko de chaque fichier récent ;
    - les dossiers de sous-agents ;
    - les fichiers d'état ;
    - l'énumération de l'app bureau (`LecteurAppBureau.cs:346-348`) ;
    - deux magasins JSON ;
    - un appel Win32 et `Process.GetProcessById`.
  - **Mesure, borne basse** : la seule énumération et le filtrage de 1 898 fichiers et 222 dossiers prennent **59 à 98 ms** par passe en PowerShell. Il faut y ajouter l'analyse JSON, non mesurée.
- **Constat** : plusieurs dizaines de millisecondes d'E/S sur le thread UI toutes les 2 s, proportionnelles à l'historique **total** des transcripts.
- **Refactoring proposé** : voir P-08.

#### [DS-PERF-03] Rapport de démarrage exécuté sur le thread UI
- **Axe** : performance
- **Sévérité** : **Majeur** (à profiler)
- **Preuve**
  - `App.xaml.cs:175` : `_ = …LogStartupAsync()` est appelé **depuis le thread UI**, sans `Task.Run`.
  - Aucun `ConfigureAwait(false)` dans `DiagnosticService.cs`, `LastExactUsageProvider.cs`, `CompositeUsageProvider.cs` ni `TranscriptActivityProvider.cs` (grep vide). Après `await _composite.GetAsync` (`DiagnosticService.cs:155`), la suite reprend donc sur le contexte UI : `InventaireProcessus` (`Process.GetProcesses`, `InventaireProcessus.cs:45`), `SessionMonitor.Inspecter` (l. 469), `SourceHistoriqueDisque.LireJour` (l. 703), plusieurs lectures de fichiers, puis `File.WriteAllText` (l. 125).
  - Le chemin des Réglages, lui, prend la précaution inverse (`MainViewModel.cs:467-470` : « gèleraient la fenêtre sur le thread UI »).
- **Constat** : à chaque lancement, une partie du rapport et la doctrine de tête tournent sur le thread UI, juste après l'apparition du cadran. Dans le cas « hors ligne au démarrage avec un dernier exact vieux », cela peut inclure la passe des transcripts de DS-PERF-01, si le diagnostic obtient le sémaphore avant l'orchestrateur.
- **Refactoring proposé** : `_ = Task.Run(() => diag.LogStartupAsync());`. C'est iso-fonctionnel (voir P-03).

#### [DS-PERF-04] Reconstruction complète de la liste du widget, et lecture synchrone à l'ouverture de l'Historique
- **Axe** : performance
- **Sévérité** : **Mineur**
- **Preuve** :
  - `SessionsViewModel.cs:198-221` : `Items.Clear()` puis recréation de chaque `SessionItemVm` toutes les 2 s, ce qui ré-instancie chaque template ;
  - `HistoriqueViewModel.cs:264-268` : `RepereHebdo` lit 7 jours de journal sur le thread UI.
- **Refactoring proposé** : réconcilier `Items` par `SessionId` (mise à jour en place) ; déplacer `RepereHebdo` dans la lecture numérotée existante (l. 383-418).

### Axe 4 — Scalabilité (outil mono-utilisateur : croissance des données)

#### [DS-SCAL-01] Coûts proportionnels à l'historique total des transcripts, pas à l'activité récente
- **Axe** : scalabilité
- **Sévérité** : **Mineur**
- **Preuve** : le filtre de récence s'applique **après** l'énumération récursive complète : `TranscriptSessionSource.cs:79-85` (toutes les 2 s), `TranscriptActivityProvider.cs:118-119`, `ReconstructionTokens.cs:357` (toutes les 60 s). Mesure : 1 898 fichiers dans 222 dossiers aujourd'hui.
- **Constat** : la croissance est bornée de fait par la purge propre à Claude Code. Je ne l'ai pas vérifiée : elle dépend du réglage de l'utilisateur.
- **Refactoring proposé** : énumérer d'abord les dossiers de projet et filtrer par la date de modification du dossier, ou réutiliser l'inventaire de `ReconstructionTokens` (il trie déjà par date de modification).

#### [DS-SCAL-02] `archived.json` n'a pas de rétention
- **Axe** : scalabilité
- **Sévérité** : **Mineur**
- **Preuve** : `ArchiveStore.cs:77-89` ajoute sans jamais purger, alors que `TreatedStore.cs:127-135` applique une rétention de 24 h.
- **Constat** : croissance lente, liée aux archivages manuels uniquement.
- **Refactoring proposé** : retirer les identifiants absents de toute source depuis plus de `HorizonsSessions.ExpirationEtat`. C'est une suggestion produit, car cela change la durée de vie d'une archive.

#### [DS-SCAL-03] Index des ids gardé en mémoire
- **Axe** : scalabilité
- **Sévérité** : **Hypothèse à vérifier**
- **Preuve** : `Tokens/IndexMessages.cs:96` (`_parId.Count`) ; chargement des mois ouverts et à la demande (l. 153-206).
- **Constat** : la mémoire croît avec le nombre de messages des mois chargés. Je n'ai pas mesuré cette empreinte.

### Axe 5 — Maintenabilité

#### [DS-MAINT-01] Version embarquée 3.4.0 alors que le code est la 3.5, et l'autostart garde l'ancien exe
- **Axe** : maintenabilité (configuration de release)
- **Sévérité** : **Critique** (bloquant, point 5)
- **Preuve**
  - `Chronos.csproj:15-18` : `Version` vaut 3.4.0.
  - Pourtant le code et la doc décrivent la 3.5 : `docs/publish.md:173` (« Premier lancement de la 3.5.0 »), `scripts/README.md`, `App.xaml.cs:130-131`.
  - `AutostartService.cs:127-129` : `vCourant <= vCible → Ignore`.
  - `VersionPublieeTests.cs:20-31` garde la cohérence des 4 propriétés, mais pas la montée de version.
- **Constat** : un exe publié en l'état sous le nom `Chronos-v3.5.0.exe` embarquerait la version 3.4.0. La convergence de l'autostart verrait une version égale à celle de la cible et laisserait le raccourci sur l'exe 3.4.0. Au redémarrage, l'ancien exe reprendrait la main : verrou d'instance, puis sa propre réconciliation, qui repointe les hooks vers lui.
- **Correction proposée** : voir P-09.

#### [DS-MAINT-02] Objets dieux sur les points chauds
- **Axe** : maintenabilité
- **Sévérité** : **Majeur**
- **Preuve**
  - `MainViewModel.cs` : 875 lignes et 39 commits. Il cumule une quarantaine de propriétés observables, 15 commandes, l'arbitrage des clics avec son `DispatcherTimer` (l. 174-228), les textes de la sonde, du journal et de la reconstruction (l. 629-724), l'empreinte du cadran (l. 262-305), les catalogues de styles et de thèmes, et l'instanciation de `ReglagesViewModel` et d'un second `ReglagesHistoriqueSurDisque` (l. 469-470).
  - `DiagnosticService.cs` : 1 037 lignes et 49 commits, une méthode `BuildReportAsync` d'environ 400 lignes (l. 131-545).
  - `App.xaml.cs` : 582 lignes et 63 commits.
  - Mesure de complexité : NON MESURÉE (outil absent).
- **Constat** : les trois fichiers les plus modifiés du dépôt sont aussi les plus gros. Toute fonctionnalité visible y passe.
- **Refactoring proposé** : voir P-10.

#### [DS-MAINT-03] Commentaires et documentation qui contredisent le code
- **Axe** : maintenabilité
- **Sévérité** : **Mineur**
- **Preuve**
  - « trois composites imbriqués » : `CompositeUsageProvider.cs:17`, `LastExactUsageProvider.cs:15-16`, `ChronosOAuthUsageProvider.cs:190`, `RateLimitHeaderUsageProvider.cs:436`. `IAuthStatus.cs:8` dit « deux ». Le câblage n'en crée **qu'un** (`App.xaml.cs:503-505`).
  - `RefreshOptions.cs:4` : « La persistance settings.json arrive en Phase 6 ; ici, valeurs par défaut pré-câblées ». C'est faux : la valeur vient de `App.xaml.cs:545-550`.
  - `MainViewModel.cs:849` : « le provider relit le coffre à chaque GetAsync ». C'est faux : `ChronosTokenAuthority.cs:76`, voir DS-ARCH-01.
  - `docs/publish.md` §5 : l'autostart se repointe désormais tout seul (`AutostartService.cs:115-138`).
  - `docs/data-sources.md:120` : le « point unique » `UsageNormalization` n'est pas respecté (DS-DUP-01).
- **Refactoring proposé** : corrections de texte uniquement. C'est iso-fonctionnel.

#### [DS-MAINT-04] Reliquats de méthodes abandonnées (le point 4 dit « pas de reliquat »)
- **Axe** : maintenabilité
- **Sévérité** : **Mineur**
- **Preuve**
  - `PercentFormatter.cs:23-30` : `Format(double?, bool isEstimated)` produit « ~80 % ». Ce reliquat de l'estimation abandonnée n'est utilisé que par des tests (`WindowGaugeViewModelTests.cs:134-154`), et le préfixe « ~ » contredit la doctrine (« Le ≥ et non un ~ », `docs/data-sources.md:113`).
  - `SettingsService.Save` (`SettingsService.cs:247-263`) : aucun appelant en production, 20 appels dans les tests (`SettingsServiceEcritureTests.cs`, `SettingsServiceTests.cs`). Son contrat (écriture d'un record entier, sans fusion) diffère de `Modifier`.
  - `ChronosPaths.UsageFile` (`usage.json`, `ChronosPaths.cs:10-14`) ne sert plus qu'à ancrer le dossier.
  - `SessionHookInstaller(string? settingsPath)` (`SessionHookInstaller.cs:141-146`) n'est utilisé que par des tests (`CompositionRootTests.cs:274`, `PrudencesEcritureHooksTests.cs:209`, `SessionsTests.cs:454`) et écrit ses sauvegardes dans un autre dossier (`chronos-backups`).
- **Refactoring proposé** : supprimer `Format(bool)` et ses tests ; rendre `Save` `internal` ou faire passer les tests par `Modifier` ; renommer `UsageFile` en `DossierChronos` ; déplacer le constructeur de test dans un helper de tests. C'est iso-fonctionnel pour la production.

#### [DS-MAINT-05] Densité de commentaires et références internes non résolubles
- **Axe** : maintenabilité
- **Sévérité** : **Mineur**
- **Preuve** : 7 237 lignes de commentaire pur sur 23 608 (31 %). On compte 1 120 occurrences d'identifiants de la forme `HDR-03`, `CPT-02` ou `D-35-08`, dont 88 décisions `D-xx-yy` distinctes. Ces identifiants ne sont définis ni dans le code ni dans `docs/`.
- **Constat** : pour un nouveau développeur, une part notable des « pourquoi » renvoie à des documents de planification qui ne font pas partie du livrable. Le test des « 10 minutes » en pâtit.
- **Refactoring proposé** : remplacer progressivement l'identifiant par la phrase de justification. Pour les invariants vraiment structurants (porteurs par référence, ordre de démarrage, autorité unique), créer `docs/decisions.md`, un ADR court.

#### [DS-MAINT-06] Build non verrouillé
- **Axe** : maintenabilité (reproductibilité)
- **Sévérité** : **Mineur**
- **Preuve** : pas de `global.json` (le SDK 10.0.201 est pris par défaut), pas de `packages.lock.json`, pas de CI (`.github/` absent). Les versions NuGet sont en revanche figées dans les csproj.
- **Refactoring proposé** : un `global.json` qui fixe le SDK (`rollForward: latestFeature`), et `RestorePackagesWithLockFile=true`.

---

## Plan de refactoring priorisé (propositions autonomes, rien n'est appliqué)

Ordre suivi : Critiques d'abord (bloquants), puis Majeurs regroupés par zone, puis Mineurs.
P-01, P-02, P-05 et P-09 **changent le comportement** : ce sont des corrections ou des suggestions produit, signalées
comme telles. Les autres visent l'iso-fonctionnalité.

### P-01 — L'autorité oublie ses jetons à la déconnexion et au login (DS-ARCH-01) — correction, effort S
```diff
 // ViewModels/MainViewModel.cs
 [RelayCommand]
 private async Task LoginClaude()
 {
-    if (_oauthLogin.IsLoggedIn) _oauthLogin.Logout();
-    else await _oauthLogin.LoginAsync();
-    IsLoggedIn = _oauthLogin.IsLoggedIn;
-    _orchestrator.RequestRefresh();
+    if (_oauthLogin.IsLoggedIn) _oauthLogin.Logout();
+    else if (!await _oauthLogin.LoginAsync()) { IsLoggedIn = _oauthLogin.IsLoggedIn; return; }
+    _authStatus.ReinitialiserApresLogin();   // la copie mémoire de l'autorité est oubliée, le coffre relu
+    IsLoggedIn = _oauthLogin.IsLoggedIn;
+    _orchestrator.RequestRefresh();
 }
```
```diff
 // Services/ChronosTokenAuthority.cs — un rafraîchissement EN VOL ne doit pas ressusciter le coffre
+private int _generation;
 public void ReinitialiserApresLogin()
 {
+    Interlocked.Increment(ref _generation);
     _jetons = null; ...
 }
 // dans GetAccessTokenAsync, avant l'appel réseau :
+var generation = Volatile.Read(ref _generation);
 var res = await _client.RefreshAsync(_jetons.RefreshToken, ct);
+if (generation != Volatile.Read(ref _generation)) return null;   // déconnexion/login survenus pendant l'appel
```
- **Tests à écrire d'abord**
  - `ChronosTokenAuthorityTests` : coffre enregistré, puis `GetAccessTokenAsync`, puis `coffre.Clear()` et `ReinitialiserApresLogin()`, puis une échéance qui force le rafraîchissement. Attendu : `null` et `File.Exists(coffre) == false`.
  - Variante « nouveau login » : après la réinitialisation, c'est le nouveau jeton qui est servi.
  - `MainViewModelTests` : `LoginClaudeCommand` en état connecté donne `FakeAuthStatus.ReinitCount == 1`.
- **Tests existants qui doivent rester verts** : `MainViewModelTests.cs:620-710` et `ChronosTokenAuthorityTests.cs:484-497`.
- **Risque résiduel** : l'ordre des deux écrivains du coffre (le login et l'autorité) reste implicite. Une option serait que `OAuthLogin` passe par l'autorité pour écrire.

### P-02 — « Encore valide » affiché comme une borne, et journal périmé refusé (DS-ARCH-02) — suggestion produit, effort S
```diff
 // Text/PercentFormatter.cs
-string prefixe = provenance == Chronos.Models.ProvenanceReleve.PlancherAvecActivite ? "≥ " : "";
+// Un relevé vieilli est une borne inférieure : le pool est partagé avec des consommateurs que les transcripts ne voient pas.
+string prefixe = provenance is ProvenanceReleve.PlancherAvecActivite or ProvenanceReleve.EncoreValide ? "≥ " : "";
```
```diff
 // Services/SourceActiviteMemoisee.cs — ne jamais servir un journal plus vieux que la validité
-catch when (_journal is not null)
-{
-}
+catch when (_journal is not null && EncoreValide(_journal, _horloge.UtcNow)) { }
```
Ensuite, mettre à jour `docs/data-sources.md` §3 (l. 108-109) et le README (l. 21).
- **Tests à écrire d'abord**
  - `PercentFormatter.Format(0.42, EncoreValide) == "≥ 42 %"`.
  - `SourceActiviteMemoisee` : un échec de l'inner après expiration doit lever, ce que `LastExactUsageProvider` traduit en journal `null`, donc en « Indisponible ».
- **Tests à adapter** : ceux qui figent « sans signe » pour `EncoreValide` (à repérer avec `grep EncoreValide tests`).
- **Variante, si l'utilisateur refuse** : il faut écrire noir sur blanc l'hypothèse « Claude Code seul consommateur » dans AUDIT_POINTS.md (point 1) et dans `docs/data-sources.md` §4.

### P-03 — Le diagnostic lit le dernier snapshot, sans rappeler la chaîne, et démarre hors du thread UI (DS-ARCH-03, DS-PERF-03) — refactoring, effort M
```diff
 // Services/RefreshOrchestrator.cs
+public UsageSnapshot? DernierSnapshot { get; private set; }
 var snap = await _provider.GetAsync(stoppingToken).ConfigureAwait(false);
+DernierSnapshot = snap;
 SnapshotChanged?.Invoke(this, snap);
```
```diff
 // Services/DiagnosticService.cs — l'appel ne sert plus qu'à attendre la première charge
-try { affiche = await _composite.GetAsync(ct); }
+try { affiche = await _instantane.AttendrePremierAsync(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false); }
```
```diff
 // App.xaml.cs:175
-_ = _host.Services.GetRequiredService<DiagnosticService>().LogStartupAsync();
+var diag = _host.Services.GetRequiredService<DiagnosticService>();
+_ = Task.Run(() => diag.LogStartupAsync());
```
- **Iso-fonctionnalité attendue** : la section « Ce qui est affiché maintenant » décrit le snapshot effectivement affiché, ce qui est plus fidèle que l'actuel. L'ordre critique documenté à `DiagnosticService.cs:145-153` reste respecté : l'état est peuplé par l'orchestrateur avant la lecture.
- **Tests existants** : la suite `DiagnosticServiceTests` (2 221 lignes) doit rester verte. Ses tests qui comptent les sondes (« deux micro-requêtes au lieu d'une ») deviennent « zéro micro-requête due au diagnostic ».
- **Test à écrire** : un faux provider qui compte ses appels, l'orchestrateur et le diagnostic, puis `LogStartupAsync`. Attendu : exactement un `GetAsync`.
- **Risque** : si l'orchestrateur n'a rien publié dans le délai, le rapport doit dire « pas encore de snapshot », jamais inventer.
- **Complément minimal, si P-03 est jugé trop large** : un `SemaphoreSlim` dans `LastExactUsageProvider.GetAsync`, plus un nom temporaire unique dans `LastExactStore` (P-12).

### P-04 — Tracker sous verrou, et lecture passive pour le diagnostic (DS-ARCH-04) — refactoring, effort S
```diff
 // Services/SessionTreatmentTracker.cs
+private readonly object _verrou = new();
 public void Observe(...)
-{
+{ lock (_verrou) {
     ...
-}
+} }
 public CauseTraitement? CauseDe(...) { lock (_verrou) { ... } }
```
```diff
 // Services/SessionMonitor.cs
-public LectureSessions Inspecter(System.DateTimeOffset now)
+public LectureSessions Inspecter(System.DateTimeOffset now, bool observer = true)
 ...
-try { _tracker?.Observe(arbitrage.Vainqueurs, now, contexte); } catch { }
+if (observer) { try { _tracker?.Observe(arbitrage.Vainqueurs, now, contexte); } catch { } }
 // DiagnosticService.cs:469 → Inspecter(_clock.UtcNow, observer: false)
```
- **Tests à écrire** : deux threads qui appellent `Observe` en boucle, sans exception et avec un état final cohérent ; le diagnostic ne modifie pas `treated.json` (`TreatedStore.DerniereEcriture` inchangée).
- **Tests existants** : `TreatedSessionsTests`, `SessionsTests`, `InspectionSessionsTests`.
- **Iso-fonctionnalité** : le verrou est iso. `observer:false` change un effet de bord non voulu du diagnostic.

### P-05 — Resets 5 h : n'afficher que le reset serveur (DS-ARCH-05) — suggestion produit, effort S
Soit `DayTimeline.ResetAngles` ne rend que l'angle du reset serveur, et les sous-tirets restent une graduation horaire neutre. Soit les projections reçoivent un style distinct (pointillé) et la doc les nomme « projections » (README l. 18, `docs/data-sources.md` §3). À trancher par l'utilisateur.

### P-06 — Un seul lecteur de ligne de transcript pour les tokens (DS-DUP-01) — refactoring, effort M
Créer `Services/Transcripts/LigneTranscript.cs` :
```csharp
internal static class LigneTranscript
{
    public static bool EstAssistant(JsonElement o) { /* corps unique, repris de LecteurTranscript.cs:221-229 */ }
    public static DateTimeOffset? Horodatage(JsonElement o)
        => o.TryGetProperty("timestamp", out var t) && t.ValueKind == JsonValueKind.String
           ? UsageNormalization.InstantDepuisIso(t.GetString()) : null;
}
```
`TranscriptActivityProvider.cs:80-83` et `LecteurTranscript.cs:184-190` l'appellent tous les deux. Chacun garde **explicitement** sa politique des horodatages futurs.
- **Iso-fonctionnalité** : quasi totale. Seule différence : les horodatages antérieurs à 2020 seront ignorés par `TranscriptActivityProvider`, comme ils le sont déjà par les agrégats.
- **Tests** : `TranscriptActivityProviderTests`, `LecteurTranscriptTests`, `ReconstructionTokensTests`. Ajouter un test de caractérisation sur un jeu de lignes commun pour vérifier que les deux lecteurs retiennent les mêmes messages.

### P-07 — Activité des transcripts : lecture incrémentale (DS-PERF-01) — refactoring, effort M/L, à profiler d'abord
- **Étape 1** (S) : découpler la validité de la mémoïsation du tick. Par exemple, invalider seulement si la taille ou la date de modification d'un fichier a changé : comparer `(chemin, taille, date)` au jeu mémorisé avant de relire. C'est iso-fonctionnel, puisque le journal rendu est pur.
- **Étape 2** (M) : lecture incrémentale par offset, sur le modèle de `Curseurs`. On conserve les entrées dédupliquées en mémoire (8 jours) et on ne relit que les octets ajoutés.
- **Test de caractérisation** : le journal produit par la version incrémentale est égal à celui de la passe complète, sur un dossier de fixtures, après ajout de lignes.

### P-08 — Le scan des sessions hors du thread UI (DS-PERF-02, DS-PERF-04) — refactoring, effort M
```diff
 // ViewModels/SessionsViewModel.cs
-public void Refresh(DateTimeOffset now)
-{
-    var snaps = AffichageSessions.Ordonner(_monitor.Read(now));
+public void Refresh(DateTimeOffset now)
+{
+    if (Interlocked.Exchange(ref _enCours, 1) == 1) return;          // pas d'empilement si un scan dépasse 2 s
+    Task.Run(() => AffichageSessions.Ordonner(_monitor.Read(now)))
+        .ContinueWith(t => { Volatile.Write(ref _enCours, 0); if (t.IsCompletedSuccessfully) Appliquer(t.Result, now); },
+                      TaskScheduler.FromCurrentSynchronizationContext());
+}
```
Dans `Appliquer`, mettre à jour `Items` en place, par `SessionId`.
- **Prérequis** : P-04 (le tracker devient accédé depuis le pool).
- **Tests** : `SessionsTests` et `AffichageSessionsTests` via un `Refresh` synchrone gardé pour les tests (`RefreshSynchrone`).
- **Risque** : les commandes Archiver et Traiter déclenchent `Refresh`, ce qui laisse un léger délai d'affichage.

### P-09 — Version 3.5.0 (DS-MAINT-01) — correction de release, effort XS
```diff
-<Version>3.4.0</Version>
-<FileVersion>3.4.0.0</FileVersion>
-<AssemblyVersion>3.4.0.0</AssemblyVersion>
-<InformationalVersion>3.4.0</InformationalVersion>
+<Version>3.5.0</Version>
+<FileVersion>3.5.0.0</FileVersion>
+<AssemblyVersion>3.5.0.0</AssemblyVersion>
+<InformationalVersion>3.5.0</InformationalVersion>
```
- **Test à ajouter** : la version du csproj est supérieure ou égale au plus grand « Premier lancement de la X.Y.Z » cité dans `docs/publish.md`. Cela empêche de documenter une version sans la monter.

### P-10 — Découper `MainViewModel` et `DiagnosticService` (DS-MAINT-02) — refactoring, effort L
- **`MainViewModel`** : extraire trois sous-VM composés, sur le modèle de `Reglages` (exposé en propriété) :
  - `EtatSourcesViewModel` : sonde, journal, reconstruction, infobulle (l. 629-759) ;
  - `ApparenceViewModel` : styles, thèmes, orientation, empreinte (l. 237-429) ;
  - `GesteCentreViewModel` : arbitre et minuterie (l. 170-228).
  Le XAML se relie par `{Binding Apparence.…}`. Injecter `ReglagesViewModel` par DI plutôt que l'instancier.
- **`DiagnosticService`** : une interface `ISectionDiagnostic { void Ecrire(StringBuilder sb, ContexteRapport c); }` et une classe par section. `BuildReportAsync` n'est plus qu'une boucle.
- **Tests** : `MainViewModelTests`, `CadranBindingTests`, `ReglagesBindingTests` et `DiagnosticServiceTests`, dont les assertions textuelles servent ici de tests de caractérisation.
- **Risque** : le volume de bindings XAML à déplacer. À faire en plusieurs commits, une section à la fois.

### P-11 — Mineurs (iso-fonctionnels, effort XS à S chacun)
- DS-ARCH-06 : déplacer les enums de préférence et `BilanRetention` vers `Models/`.
- DS-DUP-02 : `SettingsService` appelle `QuarantaineFichier.Mettre`.
- DS-MAINT-03 : corriger les 5 commentaires et `docs/publish.md` §5.
- DS-MAINT-04 : retirer les reliquats listés.
- DS-MAINT-05 : écrire `docs/decisions.md`.
- DS-MAINT-06 : ajouter `global.json` et un lockfile.
- DS-SCAL-01 : filtrer l'énumération par la date de modification des dossiers.

### P-12 — Écriture atomique commune (DS-DUP-03) — effort S
Créer `EcritureAtomique.Ecrire(chemin, octets)` avec un nom temporaire `.tmp-{pid}-{guid}` et les reprises sur `Move` de `SettingsService.cs:361-372`, utilisée par les 7 magasins. C'est iso-fonctionnel. Tests : ceux de chaque magasin.

---

## Suggestions produit hors périmètre (changements de fonctionnalité relevés en passant)

1. **P-02** : afficher « ≥ » pour un relevé « encore valide », ou bien écrire formellement l'hypothèse « Claude Code seul consommateur ».
2. **P-05** : resets 5 h projetés sur la timeline 24 h, à retirer ou à marquer comme projections.
3. **DS-SCAL-02** : donner une rétention aux archives de sessions.
4. **Commentaires JSONC perdus** à la réécriture de `~/.claude/settings.json` : prévenir dans le bilan de réconciliation quand des commentaires sont détectés (le fichier original reste dans la sauvegarde).
5. **`OAuthLogin`** est un dialogue construit en code-behind (`Views/OAuthLogin.cs:44-113`), hors du système de design des autres fenêtres. Ce point relève du design, pas de l'audit.

## Ce qui n'a pas pu être vérifié

- **Tests** : 2 152 cas listés, **non exécutés**. Pas de couverture mesurée. J'ignore si P-03 et P-04 cassent des tests de comptage de sondes : à vérifier lors de l'application.
- **Complexité cyclomatique et clones** : NON MESURÉES (`jscpd` et les métriques Roslyn sont absents). La duplication a été établie par lecture ciblée, guidée par les flux.
- **Profiling** : aucun. Les seules mesures sont des E/S en lecture seule (énumération, volumes). Les durées réelles du scan des sessions sur le thread UI et du rapport de démarrage restent **à profiler**, avec l'exe en Debug et un traceur Dispatcher.
- **Format réel des transcripts** (fichiers `agent-*.jsonl` de premier niveau, DS-DUP-04) : non vérifié, car je n'ai pas lu le contenu des transcripts.
- **Purge des transcripts par Claude Code** (borne de DS-SCAL-01) : non vérifiée.
- **Comportement d'un `refresh_token` après un nouveau login** côté serveur (DS-ARCH-01, cas « changement de compte ») : déduit du code, non observé.
- **`chronos.log` réel** : celui présent sur la machine date de la 3.4.0 (2026-09-28). Il ne peut pas servir de mesure pour la 3.5.

**Reboucler** : profiler DS-PERF-02 et DS-PERF-03 ; installer `jscpd` (avec accord) pour une mesure des clones ; lancer `dotnet test` (avec accord) avant et après chaque proposition appliquée.

## Transition

Il y a des Critiques : 3 bloquants pour la 3.5.0, à savoir **P-01**, **P-02** (ou l'acceptation écrite de l'hypothèse) et **P-09**.
On passe les propositions une par une pour validation ? Valider une proposition signifie que tu l'appliqueras, toi ou
une session d'application dédiée ; cette skill ne l'applique pas.
