# Re-vérification ciblée Fiabilité / Données — phase 42.2 (2026-10-04)

> Rapport rendu par l'agent (sans outil d'écriture), reporté par l'orchestrateur, condensé. HEAD 8d07cf4.

Phase 1 : suite 2112/2113 verts hors dépôt (seul échec : `DiagnosticServiceTests.Le_diagnostic_ne_tient_plus_sa_propre_liste_d_evenements`, :2151-2164, retrouve `src/` depuis `AppContext.BaseDirectory` → fragile hors arbre ; 2113/2113 dans le dépôt). Greps : plus aucun `Save(Load())`, aucune primitive d'écriture dans l'installateur/réconciliateur, une seule passerelle, trois filets globaux, plus de `LocationChanged`.

| ID | Verdict |
|---|---|
| FIAB-1, FIAB-2, FIAB-3/DATA-5, FIAB-4, FIAB-8=DATA-4, FIAB-9 (statique), DATA-1, DATA-3, DATA-7, DATA-13 (journal), MAT-1, MAT-3 (en substance), MAT-4, MAT-5 | FERMÉ |
| DATA-2 | PARTIELLEMENT — nominal fermé (flush index → agrégats → curseurs s'arrête au premier échec, test :681) ; extension « mois gelé » ouverte (FIAB-R4) |

## Nouveaux findings (0 B, 0 Maj, 4 Min, 2 Info)
- **FIAB-R1 — Mineur** — `PasserelleReglagesClaude.cs:43,138-143,191-195`, `SessionHookInstaller.cs:169,187` : depuis MAT-1, chaque bascule du widget consomme une des 5 sauvegardes → la sauvegarde d'avant Chronos est évincée en 3 allers-retours (aggrave DATA-8). Reco : épingler la première sauvegarde (`claude-settings-initial.json`).
- **FIAB-R2 — Mineur** — `App.xaml.cs:342,143-148`, `OverlayController.cs:183-184,216-232`, `SettingsService.cs:160,200-209` : réglages Inaccessibles au démarrage puis lisibles → `RestorePlacement` persiste `Background=false` (préférence perdue). Reco : ne persister Background que si la lecture de démarrage est fiable.
- **FIAB-R3 — Mineur** — `App.xaml.cs:217-221`, `JournalIncidents.cs:58-86`, `DiagnosticService.cs:122` : le filet Dispatcher (Handled=true) n'a ni dédoublonnage ni plafond → une exception récurrente (tick 1 s) peut écrire ~86 400 lignes/jour, relues en entier au démarrage. Reco : dédoublonner par (type, message, premier cadre), plafonner par minute, « répété N fois », lecture bornée.
- **FIAB-R4 — Mineur** (extension DATA-2) — `ReconstructionTokens.cs:273-279,305,327-338,422-437`, `MagasinAgregats.cs:195-202`, `IndexMessages.cs:247-264` : index d'un mois gelé écrit puis agrégats en échec → arrêt/rejeu → delta perdu (sous-comptage permanent). Reco : pour un mois gelé, agrégats avant ids, ou ne pas abandonner les mois gelés sales au rejeu.
- **FIAB-R5 — Info** — `MagasinMapSessions.cs:156`, `ArchiveStore.Add` : après quarantaine, repart d'un ensemble vide au lieu du dernier ensemble lu → sessions archivées qui réapparaissent. Reco : partir de `_dernierLu`.
- **FIAB-R6 — Info** — `LecteurAgregats.cs:52-53,81-82,111-112` : DATA-13 non étendu aux agrégats de tokens (inaccessible = 0 tranche). Reco : drapeau LectureIncomplete.
