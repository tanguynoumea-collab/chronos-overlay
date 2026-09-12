---
phase: 23-un-magasin-qui-ne-croit-plus-et-n-oublie-plus
verified: 2026-09-12T00:00:00Z
status: human_needed
score: 4/4 must-haves verified (mécanisme) — vérification in vivo requise pour clôturer le critère n°1 et n°2
human_verification:
  - test: "Lancer volontairement la version du dépôt (exe republié) et mesurer le dossier avant/après : ls \"$APPDATA/Chronos/sessions\" | wc -l (avant 66), *.json | wc -l (avant 54), grep -c 'tmp-' (avant 12)"
    expected: "0 fichier temporaire après le lancement ; seuls les états de moins de 72 h ou attestés vivants subsistent (ordre de grandeur : quelques unités sur 54)"
    why_human: "Le test unitaire prouve le mécanisme (BalayageMagasinSessionsTests), pas que le vrai dossier utilisateur a changé — précédent 21-04 (archived.json). La phase s'est explicitement interdit de lancer/tuer l'overlay pid 119412 (exe v3.0.2, antérieur à ce milestone)."
  - test: "Fermer brutalement un terminal Claude Code (aucune valeur SessionEnd.reason ne couvre ce cas), vérifier que le fichier d'état reste, puis qu'il a disparu au lancement suivant une fois 72 h passées"
    expected: "Le fichier d'état créé par le hook survit à la fermeture brutale, puis disparaît sans être déclaré terminé/traité après expiration"
    why_human: "Nécessite un vrai processus Claude Code et un vrai terminal tué ; non simulable en test unitaire sans invoquer un hook réel"
  - test: "Avec le widget affiché (lecteur toutes les 2 s), enchaîner des tours dans une session réelle et constater qu'aucun changement d'état n'est manqué ; en cas d'échec, claude --debug doit montrer la ligne d'erreur du hook sans bloquer la session"
    expected: "Chaque changement d'état apparaît dans le widget ; un échec d'écriture (le cas échéant) est visible sur stderr et la session continue"
    why_human: "Comportement temps réel avec un vrai processus Claude Code ; le canal stderr et l'absence de blocage sont vérifiés unitairement (code de sortie 1, jamais 2) mais l'expérience utilisateur bout-en-bout ne l'est pas"
  - test: "Observer à l'œil qu'une session balayée disparaît simplement du widget, sans jamais réapparaître marquée « traitée » ou « terminée »"
    expected: "Disparition silencieuse, aucun verdict affiché"
    why_human: "Rendu visuel du widget, non couvert par un test automatisé"
  - test: "Rien de ce milestone ne s'exécute tant que l'exe n'est pas republié : les hooks de ~/.claude/settings.json pointent Chronos-v3.0.2.exe (pid 119412), antérieur à toute la phase 23"
    expected: "Après republication (version dans l'exe et dans le nom du fichier), le nouveau code (écriture directe + balayage) devient actif"
    why_human: "Étape de déploiement hors du périmètre de vérification du code ; nécessite une action explicite de l'utilisateur (publish + redémarrage des hooks)"
---

# Phase 23 : Un magasin qui ne croît plus et n'oublie plus — Verification Report

**Phase Goal:** `%APPDATA%\Chronos\sessions` cesse d'être un dépotoir qui ne fait que grandir, et une écriture
d'état de hook cesse de pouvoir disparaître sans que personne ne le sache.
**Verified:** 2026-09-12
**Status:** human_needed
**Re-verification:** Non — vérification initiale

## Goal Achievement

### Observable Truths (les 4 critères de succès du ROADMAP)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Le magasin se résorbe tout seul (CYC-01) | ✓ VERIFIED (mécanisme) / ? human pour le dossier réel | `BalayageMagasinSessions.Balayer()` existe, câblé dans `OnStartup` sur `GetRequiredService<SessionMonitor>().Directory` (grep → 1), 8 tests verts dont le rejeu exact du cas mesuré (40j/10j/10h/1h → 2 retirés, 2 conservés ; 5 débris même session_id → 5 retirés, 1 frais épargné). Le vrai dossier `%APPDATA%\Chronos\sessions` (66 entrées) est resté intact — c'est volontaire, l'overlay v3.0.2 en cours n'exécute pas ce code |
| 2 | Un terminal tué ne laisse plus de trace éternelle (CYC-01) | ✓ VERIFIED (mécanisme) | `grep -c "SessionEnd" src/Chronos/Services/BalayageMagasinSessions.cs` → 0 : le balayage ne consulte aucun événement de fin, seuls âge + attestation de vie tranchent. Non observable in vivo dans cette session (nécessite de tuer un terminal réel et d'attendre 72 h) |
| 3 | Une écriture qui échoue se voit (CYC-02) | ✓ VERIFIED | `EcritureEtatSession.Appliquer` : écriture directe (`FileMode.Create`, 1 occurrence), 200 écritures sous lecteur `FileShare.ReadWrite` → 0 échec, dernier `updated_at` sur disque = dernier écrit (test exécuté et vert). `grep -c "return 2;" src/Chronos/App.xaml.cs` → **0** confirmé. `RunSessionHook` rend 0/1, signale la cause réelle sur `OpenStandardError` en UTF-8 |
| 4 | Le balayage ne ment pas (le plus important) | ✓ VERIFIED — DEUX preuves de natures différentes | (a) Réflexion : `Le_balayage_ne_connait_aucun_magasin_de_verdict` — aucun champ/paramètre `TreatedStore`/`ArchiveStore` (0 usage de code, 2 occurrences XML-doc uniquement), `IClock` exigé au constructeur. (b) Comportement : `Un_etat_balaye_disparait_sans_etre_declare_traite_ni_archive` — après balayage, `Inspecter` rend `Visibles` vide ET `Masquees` vide, aucun fichier de verdict créé. (c) Critère double : `Une_session_vivante_depuis_des_jours_survit_au_nettoyage` — un état de 40 jours attesté vivant survit (`EtatsRetires == 0`) |

**Score:** 4/4 truths vérifiées au niveau mécanisme/code ; 2 des 4 (n°1, n°2) comportent un volet in vivo explicitement non exécutable dans cette vérification (overlay non lancé, par construction du milestone).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/EcritureEtatSession.cs` | Écriture directe testable + résultat porteur de sa cause | ✓ VERIFIED | 72 lignes (≥60). `FileMode.Create`→1, `FileShare.Read)`→1, `File.Move`→0, `.tmp-`→0, `catch { }`→0, `FromUnixTimeMilliseconds`→0. Contenu relu et conforme exactement au plan |
| `tests/Chronos.Tests/EcritureEtatSessionTests.cs` | Scénario lecteur concurrent × 200 + échec rendu | ✓ VERIFIED | 166 lignes (≥90), 6 tests présents et verts, tous en dossier temporaire avec garde `Assert.StartsWith(Path.GetTempPath()...)` |
| `src/Chronos/Services/BalayageMagasinSessions.cs` | Deux gestes distincts, horloge injectée, critère double | ✓ VERIFIED | 146 lignes (≥90). Constructeur `(string dossier, ISessionSource attestationDeVie, IClock horloge)`→1 occurrence exacte. `_horloge.UtcNow`→1, `DateTimeOffset.UtcNow`→0, `TreatedStore`→0, `ArchiveStore`→2 (XML-doc seul), `SessionActivity`→0, `SessionEnd`→0, `UsageNormalization.InstantDepuisEpochMillisecondes`→1 |
| `tests/Chronos.Tests/BalayageMagasinSessionsTests.cs` | Critère double + preuve que balayer ne conclut rien | ✓ VERIFIED | 255 lignes (≥130), 8 tests présents et verts (âge+attestation, survie 40j, débris×5+frais, illisible daté par écriture, rien-à-retirer-ne-touche-rien, doctrine, réflexion, dossier absent) |
| `.planning/phases/23-.../23-VALIDATION.md` | Carte de vérification remplie et mesurée | ✓ VERIFIED | `status: validated`, `(à mesurer)`→0 occurrence, section « À VÉRIFIER PAR L'UTILISATEUR » présente, les 4 critères du ROADMAP avec preuves nommées et résultats |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `App.xaml.cs` (mode --hook) | `EcritureEtatSession.Appliquer` | `RunSessionHook` | ✓ WIRED | `grep -c "EcritureEtatSession.Appliquer(" App.xaml.cs` → 1, appelé dans `RunSessionHook`, résultat consommé (`ecriture.Reussi`) |
| `App.xaml.cs` (mode --hook) | flux d'erreur processus | `OpenStandardError` + UTF8 strict | ✓ WIRED | `grep -c "OpenStandardError"` → 1 ; `SignalerSurErreurStandard` appelée sur échec, jamais via `Console.Error` |
| `App.xaml.cs` (`OnStartup`) | `BalayageMagasinSessions.Balayer` | `GetRequiredService<BalayageMagasinSessions>().Balayer()` | ✓ WIRED | Présent littéralement (1 occurrence), appelé en mode overlay uniquement, dans un `try/catch` best-effort, après `PurgerPrefixe("desktop:")` et avant `OfferOnFirstRun()` |
| DI (`App.xaml.cs`) | `SessionMonitor.Directory` | dérivation du dossier du moniteur, pas un second chemin | ✓ WIRED | `GetRequiredService<SessionMonitor>().Directory` → 1 occurrence dans l'enregistrement du balayeur ; `GetRequiredService<SessionMonitor>()` total = 3 (contrôleur widget, diagnostic, balayeur) |
| `BalayageMagasinSessions` | `ISessionSource` (attestation de vie) | constructeur, lu une fois par `Balayer()` | ✓ WIRED | `TranscriptSessionSource` injectée en production ; le test `Une_session_vivante_depuis_des_jours_survit_au_nettoyage` prouve que l'attestation empêche le balayage |

### Data-Flow Trace (Level 4)

Non applicable au sens strict (pas de composant UI affichant des données dynamiques dans cette phase), mais le
raisonnement équivalent a été appliqué à la chaîne suppression → lecture :
- `EcritureEtatSession.Appliquer` écrit réellement sur disque (`FileStream` + `Flush`) — vérifié par le test
  `Le_contenu_ecrit_est_relisible_par_le_moniteur` qui relit via un vrai `SessionMonitor`.
- `BalayageMagasinSessions.Balayer` supprime réellement des fichiers (`File.Delete`), et le test de doctrine
  vérifie ensuite avec un vrai `SessionMonitor.Inspecter` que la session a disparu de `Visibles` ET
  `Masquees` — pas un simple bilan numérique déconnecté de l'effet réel.
- Statut : ✓ FLOWING pour les deux mécanismes.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build 0 erreur / 0 avertissement | `dotnet build Chronos.sln -c Debug --nologo -v q` | "0 Avertissement(s), 0 Erreur(s)" | ✓ PASS |
| Suite complète, run 1 | `dotnet test Chronos.sln -c Debug --nologo -v q` | 763/763, 0 échec, 5 s | ✓ PASS |
| Suite complète, run 2 (stabilité) | idem | 763/763, 0 échec, 5 s | ✓ PASS |
| Gardes ciblées (NormalisationUnique, ServicesLayerPurity, CompositionRoot, GardesDoctrine, GardesPerimetre) | `--filter "FullyQualifiedName~..."` | 26/26, 0 échec | ✓ PASS |
| `return 2;` dans App.xaml.cs | `grep -c "return 2;"` | 0 | ✓ PASS |
| `DiagnosticService.cs` absent du diff de phase | `git diff --name-only 025538f..HEAD -- src/ tests/` | 7 fichiers listés, `DiagnosticService.cs` absent | ✓ PASS |
| `TreatedStore.cs` / `ArchiveStore.cs` / `SessionMonitor.cs` absents du diff | idem | absents | ✓ PASS |
| Mutation `MUTANT` résiduelle dans le code source | `grep -rn "MUTANT" src/ tests/` (hors binaires) | 0 occurrence source | ✓ PASS |
| Sécurité : sessions = 66, .json = 54, tmp = 12 | `ls`/`grep` sur `%APPDATA%\Chronos\sessions` | 66 / 54 / 12 | ✓ PASS |
| Sécurité : archived.json = 84 octets | `stat -c '%s'` | 84 | ✓ PASS |
| Sécurité : oauth.dat = 518 octets (taille seule) | `stat -c '%s'` | 518 | ✓ PASS |
| Overlay pid 119412 vivant, non relancé | `tasklist /FI "PID eq 119412"` | `Chronos-v3.0.2.exe` présent | ✓ PASS |
| `git status --porcelain` / diff `*.csproj` | `git status --porcelain` / `git diff -- '*.csproj'` | vide / vide | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|--------------|--------|----------|
| CYC-01 | 23-02-PLAN.md | Balayage des états expirés + .tmp orphelins | ✓ SATISFIED | `BalayageMagasinSessions` + 8 tests + câblage démarrage, vérifiés ci-dessus |
| CYC-02 | 23-01-PLAN.md | Écriture d'un état de hook non perdue en silence | ✓ SATISFIED | `EcritureEtatSession` + 6 tests + `RunSessionHook` (code retour 0/1, jamais 2) |

Aucune exigence orpheline : REQUIREMENTS.md ne mappe que CYC-01 et CYC-02 sur la Phase 23, toutes deux
déclarées dans les frontmatters des deux plans.

### Anti-Patterns Found

Aucun anti-pattern bloquant ou d'avertissement détecté dans les fichiers modifiés de la phase :
- Aucun `TODO`/`FIXME`/`PLACEHOLDER` dans `EcritureEtatSession.cs` ni `BalayageMagasinSessions.cs`.
- Aucun `catch { }` vide dans le chemin d'écriture (celui de `App.xaml.cs`/`EcritureEtatSession.cs` porte
  systématiquement soit une action de compensation documentée, soit `catch (Exception ex)` avec traitement).
  Les `catch { }` restants dans `BalayageMagasinSessions.cs` sont volontaires et documentés (best-effort,
  ne doit jamais empêcher un démarrage) — comportement voulu, pas un défaut avalé silencieusement.
- `ArchiveStore.Add` conserve son écriture par fichier temporaire (`File.Move` présent, non modifié) —
  documenté comme un arbitrage assumé et hors périmètre (aucun canal pour rendre un échec constatable).

**Point d'attention méthodologique (non bloquant) :** les deux mutations de falsification (23-01 et 23-02)
ont nécessité un alias temporaire (`AppliquerMUTANT`, `BalayerMUTANT`) car la mutation littérale ne compilait
pas. Analyse : les deux gardes visées sont des gardes **textuelles** (elles font `Assert.Contains`/
`Assert.DoesNotContain` sur le texte source de `App.xaml.cs`), pas des tests comportementaux en exécution.
L'alias servait uniquement à garder l'assembly compilable pour que `dotnet test` puisse s'exécuter — la
mutation du **texte** du site d'appel (`AppliquerMUTANT(` / `.BalayerMUTANT()`) a bien fait échouer les
assertions textuelles ciblées, ce qui est exactement ce qui devait être prouvé. Le procédé est donc valide
pour ces gardes spécifiques ; il ne serait pas suffisant pour un test comportemental en exécution, mais ce
n'était pas le cas ici.

### Human Verification Required

Voir le frontmatter `human_verification`. Résumé :

1. Confirmer in vivo que le dossier réel `%APPDATA%\Chronos\sessions` (66 entrées : 54 `.json` + 12 `.tmp`)
   se résorbe au prochain lancement volontaire de l'exe republié.
2. Confirmer in vivo qu'un terminal tué ne laisse pas de trace éternelle (fichier créé, survit, disparaît
   après 72 h).
3. Confirmer en usage réel que l'échec d'écriture (rare, ~0,7/5000) est bien visible via `claude --debug`
   sans bloquer la session.
4. Confirmer à l'œil que le widget ne montre jamais une session balayée comme « traitée » ou « terminée ».
5. Republier l'exe (version dans le binaire + nom de fichier) : rien de cette phase ne s'exécute chez
   l'utilisateur tant que `Chronos-v3.0.2.exe` (pid 119412) reste la version active.

### Gaps Summary

Aucun gap de code détecté : les artefacts existent, sont substantiels, câblés, et leurs tests passent
(763/763 sur deux exécutions consécutives). Toutes les contraintes de rédaction demandées par les deux plans
(occurrences `grep` exactes, absence de fichiers hors périmètre dans le diff, horloge injectable à 0
occurrence système, garde par réflexion + garde comportementale pour la doctrine « expirer = ne plus
savoir ») ont été vérifiées directement dans le code réel et correspondent exactement aux valeurs annoncées
dans les SUMMARY. Les invariants de sécurité mesurés sur la machine (66/54/12, 84 octets, 518 octets, pid
119412 vivant) sont conformes.

Le statut `human_needed` (plutôt que `passed`) reflète uniquement le fait que deux des quatre critères du
ROADMAP (n°1 et n°2) comportent un volet **in vivo** — la résorption réelle du dossier utilisateur et la
survie/disparition d'un état après un terminal tué — que cette vérification ne peut pas exécuter sans lancer
l'overlay sur les données réelles, ce que la phase elle-même s'est explicitement interdit de faire. Ce n'est
pas un gap de code : le mécanisme est prouvé unitairement et par des mesures exactes du cas réel rejoué en
test ; c'est une confirmation opérationnelle qui appartient à l'utilisateur, après republication de l'exe.

---

*Verified: 2026-09-12*
*Verifier: Claude (gsd-verifier)*
