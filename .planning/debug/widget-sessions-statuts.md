---
status: diagnosed
trigger: "le widget de sessions de Chronos transmet mal les trois statuts (réfléchit / attend / traité)"
created: 2026-09-12T13:00:00Z
updated: 2026-09-12T13:40:00Z
mode: find_only (aucun correctif appliqué, aucun fichier de src/ modifié)
---

## Current Focus

hypothese: enquête close — les trois états ont chacun une cause racine distincte et prouvée.
test: toutes les sondes sont passées (dossier jetable, aucune écriture dans src/ ni dans les données réelles)
expecting: n/a
next_action: remettre le rapport à l'utilisateur ; ne rien corriger sans sa décision.

## Symptoms

expected: le widget répond d'un coup d'œil à « quelle session m'attend ? », avec trois états fiables
  (réfléchit / attend une réponse / traité → disparaît).
actual: statuts « bancals » ; sessions mortes visibles ; états qui ne correspondent pas à la réalité ;
  le « traité » ne fait pas disparaître ce qu'il devrait.
errors: aucun message d'erreur — toutes les défaillances sont SILENCIEUSES (catch vides).
reproduction: usage normal ; état observable en continu dans %APPDATA%\Chronos\sessions et treated.json.
started: problème ancien, jamais élucidé. Widget bâti hors GSD (v2.5), étendu au milestone v1.4 (UIA bureau).

## Eliminated

- hypothese: « les 25 hooks cumulés (5 versions d'exe) expliquent TOUT le désordre »
  evidence: les 5 fichiers .tmp du même session_id portent 5 horodatages ESPACÉS DE PLUSIEURS HEURES
    (1783771135994 … 1783870304666), pas 5 copies d'un même événement. Ce sont 5 événements distincts.
    De plus le fan-out écrit un contenu IDENTIQUE : au moins un Move réussit (mesuré : 425/1000 réussis
    à 5 écrivains simultanés), donc l'état final restait correct. Le cumul a produit des DÉBRIS,
    pas la corruption des statuts.
  timestamp: 2026-09-12T13:10Z

- hypothese: « le hook SessionEnd est tué par le budget de 1,5 s »
  evidence: l'installateur pose "timeout": 10 sur chaque groupe, ce qui relève le budget à 10 s
    (doc officielle). Latence mesurée de Chronos-v3.0.2.exe --hook sur la vraie machine : 584–611 ms.
    Le budget n'est pas le mode de défaillance.
  timestamp: 2026-09-12T13:15Z

- hypothese: « la course d'écriture tmp+Move est la cause quotidienne des statuts faux »
  evidence: mécanisme RÉEL et reproduit (58 % des Move perdus sous lecteur serré), mais le rapport
    cyclique mesuré sur les vraies données est de 0,014 % par fichier et par écriture
    (passe de lecture = 15,15 ms pour 54 fichiers, cadence 2 s) → ~0,7 événement perdu sur 5000.
    Défaut réel, contributeur MINEUR. Ne pas lui attribuer la douleur quotidienne.
  timestamp: 2026-09-12T13:20Z

- hypothese: « les transcripts de sous-agents apparaissent comme des sessions fantômes »
  evidence: les 817 fichiers agent-*.jsonl portent isSidechain:true sur 100 % de leurs lignes
    (343/343 vérifiées) → Classify renvoie null. Aucune session fantôme. MAIS ils consomment
    quand même les 12 emplacements de Take(12) (voir Evidence).
  timestamp: 2026-09-12T13:30Z

- hypothese: « le schéma des transcripts a changé (type:assistant devenu type:message) »
  evidence: faux positif de ma première mesure (extraction textuelle du PREMIER "type" de la ligne,
    qui attrapait le "type":"message" imbriqué). Comptage par parsing JSON réel : type racine
    = assistant / user / attachment / tool_result. Le contrat lu par TranscriptSessionSource tient.
  timestamp: 2026-09-12T13:32Z

## Evidence

- timestamp: 2026-09-12T13:05Z
  checked: %APPDATA%\Chronos\sessions — 54 .json + 12 .tmp orphelins
  found: SEULE suppression de fichier d'état dans tout src/ = App.xaml.cs:128, sur l'événement SessionEnd.
    Aucun balayage d'expiration, aucun nettoyage des .tmp, nulle part.
  implication: le magasin disque ne décroît JAMAIS. Q1 tranchée.

- timestamp: 2026-09-12T13:12Z
  checked: File.Move(tmp, dst, overwrite:true) contre un lecteur FileStream(…, FileShare.ReadWrite)
  found: UnauthorizedAccessException (0x80070005), le .tmp reste, la mise à jour est PERDUE en silence.
    Ajouter FileShare.Delete au lecteur NE CORRIGE PAS (200/500 échecs persistants).
    5 retries × 15 ms ramènent l'échec à 3/500 ; l'écriture DIRECTE (FileMode.Create, FileShare.Read)
    à 0/500. Le nom du .tmp étant indexé par PID, les orphelins se recouvrent : 12 orphelins
    sous-estiment massivement le nombre d'écritures perdues.
  implication: tmp+Move est la PIRE stratégie ici. Q6 tranchée : perte d'événements prouvée
    mécaniquement, corruption de l'état survivant non démontrée sur les données réelles.

- timestamp: 2026-09-12T13:18Z
  checked: documentation officielle des hooks Claude Code (contrat externe)
  found: (a) Stop NE SE DÉCLENCHE PAS sur interruption utilisateur ;
    (b) Notification est un événement d'ALERTE conditionné au fait que l'utilisateur « semble absent »,
        il couvre à la fois permission, inactivité ≥ 60 s ET fin de tâche ;
    (c) un hook DÉDIÉ PermissionRequest existe désormais ;
    (d) SessionEnd.reason ∈ {clear, logout, prompt_input_exit, other} — aucune valeur ne couvre
        un terminal tué ou un redémarrage machine ;
    (e) le catalogue d'événements compte aujourd'hui ~30 entrées (StopFailure, SubagentStart,
        PermissionDenied, TeammateIdle, PreCompact/PostCompact…) contre les 5 câblées par Chronos.
  implication: Q3 tranchée — la sémantique de la source est FAUSSE, pas seulement en retard.

- timestamp: 2026-09-12T13:25Z
  checked: SessionTreatmentTracker + TreatedStore, sondes S1/S2/S3 contre les classes réelles
  found: S1 — un tracker neuf (redémarrage de l'overlay) ouvre un « nouvel épisode » daté now pour toute
      session encore en attente ; cet épisode est plus récent que treatedWaitingTs → NET-03 PURGE
      l'entrée → toutes les sessions traitées RESSORTENT à chaque redémarrage.
    S2 — un SEUL cycle où la session paraît non-attente suffit à la marquer traitée pour 6 h.
    S3 — NET-02 exige Origin==Desktop ET un id « desktop:foreground:* » : une session Claude Code
      regardée 20 s au premier plan n'est JAMAIS acquittée. NET-02 est mort pour le terminal.
  implication: Q2 tranchée.

- timestamp: 2026-09-12T13:28Z
  checked: état RÉEL en lecture seule (SessionMonitor pointé sur les vraies données, tracker=null)
  found: e465420e (PROJET ADVANCED SHEET) — fichier de hook = WaitingAttention/permission_prompt figé
      depuis 625 min ; transcript écrit il y a 10 min (session VIVANTE, tour agentique unique depuis
      2026-09-11T23:27Z) ; treated.json la marque traitée. LE WIDGET LA CACHE.
    Arithmétique : treatedWaitingTs = 1789210240523, updated_at du hook = 1789181509266.
      Écart = 478 min. DropAfter = 480 min. L'épisode d'attente a été enregistré ~69 s AVANT le
      franchissement de DropAfter, puis NET-01 a tiré à l'instant exact où DropAfter a fait tomber
      le fichier de hook et où le transcript (Working) a repris la main.
  implication: PREUVE IN VIVO de la chaîne DropAfter → bascule de source → NET-01 → masquage 6 h.

- timestamp: 2026-09-12T13:34Z
  checked: TranscriptSessionSource.Read sur les vraies données
  found: 868 transcripts dont 817 agent-*.jsonl (94 %). Take(12) est appliqué AVANT le filtre
    isSidechain : chaque sous-agent chaud consomme un emplacement puis est jeté. À l'instant de la
    mesure, 4 des 5 fichiers chauds étaient des sous-agents. Démonstration : 12 sous-agents chauds
    font disparaître totalement la vraie session de la source transcripts.
  implication: la source de base s'aveugle d'elle-même pendant les vagues d'agents parallèles.

- timestamp: 2026-09-12T13:36Z
  checked: arbitrage transcript vs hook (sonde dédiée), vérité terrain = modèle en train de travailler
  found: hook Working de 25 min → « inconnu » ; hook WaitingTurn de 7 h → « tour fini » ;
    hook WaitingAttention de 7 h → « à toi ». Un signal de hook de 7 h bat un transcript de 10 s.
  implication: Q4 tranchée — l'arbitrage est un écrasement par ordre d'insertion, sans comparaison
    de fraîcheur ni trace.

- timestamp: 2026-09-12T13:38Z
  checked: DiagnosticService.cs:463 et ArchiveStore.cs
  found: le diagnostic appelle `new SessionMonitor()` (constructeur nu) → SANS filtre treated et SANS
    source bureau : il ne rapporte donc PAS ce que le widget affiche. Il liste en outre files.Take(8),
    soit les 8 premiers fichiers par ordre alphabétique, tous vieux de plusieurs semaines.
    ArchiveStore applique un TTL de 6 h alors que son contrat annoncé (NET-04) est « permanent,
    jamais réversible » — archived.json contient encore 2 entrées de juillet que Load() écarte.
  implication: l'instrument de mesure de l'utilisateur est mal calibré — d'où « jamais élucidé ».

- timestamp: 2026-09-12T13:39Z
  checked: baseline + intégrité
  found: `dotnet test Chronos.sln` → 752/752 verts en 5 s ; `git status --porcelain` vide ;
    %APPDATA%\Chronos\sessions toujours à 66 entrées (54 .json + 12 .tmp).
  implication: aucun fichier de src/ ni aucune donnée utilisateur n'a été modifié.

## Resolution

root_cause: |
  Trois causes racines distinctes, plus deux amplificateurs transverses. Voir le rapport remis.
  1. « réfléchit » : Working est une hypothèse d'expiration, pas une observation ; aucun événement ne
     confirme jamais la poursuite du travail, et le fichier de hook périmé écrase le transcript frais.
  2. « attend »   : la sémantique des événements est fausse à la source (Stop muet sur Échap ;
     Notification = alerte d'absence, pas état ; PermissionRequest non utilisé) et l'état d'attente
     est un instantané figé que rien ne réfute.
  3. « traité »   : aucune notion de traitement pour une session de terminal — NET-02 est réservé à
     l'app bureau, NET-01 confond « répondu » et « source périmée », et le traitement ne survit pas
     à un redémarrage de l'overlay.
  Amplificateurs : le magasin disque n'est jamais purgé (SessionEnd est structurellement non garanti)
  et l'arbitrage entre sources ignore la fraîcheur.
fix: (aucun — mode find_only ; correctifs proposés par écrit dans le rapport)
verification: (n/a)
files_changed: []
