---
phase: 14-auto-disparition-hysteresis
verified: 2026-07-10T00:00:00Z
status: passed
score: 4/4 must-haves verified
human_verification:
  - test: "Acquittement par focus réel de la fenêtre Claude bureau (NET-02 bout de chaîne OS)"
    expected: "Une session bureau en attente gardée au premier plan de l'app Claude (titre contenant « Claude ») ≥ ~2,5 s disparaît de la liste ; un simple survol < 2,5 s ne la fait pas disparaître."
    why_human: "WindowsForegroundWatch.IsClaudeForeground() lit le titre de la vraie fenêtre de premier plan via Win32 (GetForegroundWindow/GetWindowText). Le comportement OS n'est pas unit-testable sans une vraie fenêtre Claude ouverte. La LOGIQUE d'hystérésis derrière ce signal est, elle, entièrement prouvée par faux focus (7 tests). Seul le brin final signal-OS→bool demande une confirmation en exécution."
---

# Phase 14 : Auto-disparition hystérésis des sessions traitées — Verification Report

**Phase Goal:** Faire disparaître automatiquement de la liste les sessions traitées selon une règle d'hystérésis réversible (magasin « traitées » filtré dans SessionMonitor.Read au même endroit qu'archived ; retrait sur « répondu » ou « acquittée par focus ≥ ~2,5 s » ; réapparition sur épisode d'attente plus récent ; archivage manuel distinct et permanent).
**Verified:** 2026-07-10
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (Success Criteria ROADMAP → NET-01..04)

| # | Truth | Status | Evidence |
| - | ----- | ------ | -------- |
| 1 | **NET-01 — Disparition sur réponse** : une session en attente disparaît dès qu'elle repasse en Working | ✓ VERIFIED | `SessionTreatmentTracker.Observe` l.67-72 : `if (wasWaiting && !isWaiting) _store.Set(...)`. La disparition seule ne déclenche RIEN (état conservé). Tests `NET01_repondu_marque_traitee`, `NET01_disparition_seule_ne_traite_pas`, `NET01_le_monitor_masque_apres_reponse` verts. |
| 2 | **NET-02 — Disparition sur acquittement focus ≥ ~2,5 s (debounce)** | ✓ VERIFIED (logique) / human (signal OS) | Tracker l.79-88 : focus continu + attente + `now - _focusSince[id] >= FocusAckDelay(2,5 s)` ⇒ `Set` ; toute interruption `Remove` du compteur (anti-survol). Signal OS RÉEL câblé : `WindowsForegroundWatch` (Win32) injecté 7e param dans DI (App.xaml.cs l.192-198). Tests `NET02_focus_continu_2_5s_acquitte`, `NET02_interruption_focus_remet_a_zero`, `NET02_ne_declenche_pas_sans_focus`, `NET02_le_monitor_masque_apres_focus_2_5s` verts. Seul le brin signal-OS→bool renvoyé à la vérif humaine. |
| 3 | **NET-03 — Réapparition réversible + purge de l'entrée treated** | ✓ VERIFIED | Réversibilité portée par un horodatage d'ÉPISODE stable (`_waitingSince[id]=nowMs` posé seulement sur `!wasWaiting`), PAS par `UpdatedAt` volatil. Purge l.96-98 : `isWaiting && treated[id] présent && cur > tts ⇒ Remove`. Le filtre SessionMonitor l.115-116 est un simple `ContainsKey` (aucune comparaison `ts >= UpdatedAt`) → pas de réapparition bureau à chaque tick. Tests `NET03_reapparition_purge_l_entree`, `NET03_le_monitor_la_reaffiche_sur_nouvel_episode` verts. |
| 4 | **NET-04 — Archivage manuel distinct et PERMANENT** | ✓ VERIFIED | `ArchiveStore` inchangé, sans `Remove` (aucun point réversible). Filtre appliqué AVANT treated (l.115 `continue` permanent). Test de CONTRASTE `NET04_archivee_reste_masquee_meme_en_attente` : session archivée + nouvel épisode d'attente plus récent ⇒ RESTE masquée (là où une treated réapparaîtrait). Vert. |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `src/Chronos/Services/TreatedStore.cs` | Magasin réversible (Set/Load/Remove, TTL 6 h, tmp+move) | ✓ VERIFIED | 108 l. `Remove` présent (point NET-03 absent d'ArchiveStore). Purge TTL sur chaque écriture. Lecture tolérante JsonDocument. Aucun type WPF. |
| `src/Chronos/Services/SessionTreatmentTracker.cs` | Détecteur stateful pur NET-01/02/03 | ✓ VERIFIED | 103 l. Horodatage d'épisode maintenu en interne (stable), horloge injectée par argument. Wiré dans SessionMonitor.Read 2.c. |
| `src/Chronos/Services/IForegroundWatch.cs` | Seam neutre du focus | ✓ VERIFIED | Interface `bool IsClaudeForeground()`, best-effort documenté. |
| `src/Chronos/Services/WindowsForegroundWatch.cs` | Impl. OS réelle (Win32, titre « Claude ») | ✓ VERIFIED | Win32 P/Invoke pur, entièrement sous try/catch → false. Aucun HWND public (bool seul). Injecté en DI. |
| `src/Chronos/Services/SessionMonitor.cs` | Double filtre archived puis treated + Observe | ✓ VERIFIED | Params hystérésis ajoutés EN FIN, nuls par défaut (non-régression). `tracker.Observe` best-effort try/catch ; filtre archived (l.115) PUIS treated (l.116). |
| `src/Chronos/App.xaml.cs` | Câblage DI complet (focus réel injecté) | ✓ VERIFIED | l.190-198 : TreatedStore + SessionTreatmentTracker + IForegroundWatch→WindowsForegroundWatch (Singleton) déclarés AVANT SessionMonitor et injectés comme params 5/6/7. `foreground=null` dormant du plan 01 remplacé par le focus réel. |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| SessionMonitor.Read | SessionTreatmentTracker.Observe | `_tracker?.Observe(raw, foreground, now)` sur snapshots BRUTS | ✓ WIRED | l.100-103, best-effort try/catch. |
| SessionMonitor.Read | TreatedStore.Load | filtre `treatedMap.ContainsKey` | ✓ WIRED | l.111,116 — masquage réversible, après archived. |
| SessionTreatmentTracker (NET-02) | source bureau réelle | clé `desktop:foreground:<kind>` + `SessionOrigin.Desktop` | ✓ WIRED | Clé produite par `DesktopUiaSessionSource` (l.105, Phase 13) ; `IsForegroundDesktop` (tracker l.47-49) matche exactement ce préfixe/origine. |
| App DI | SessionMonitor(7 params) | `WindowsForegroundWatch` injecté | ✓ WIRED | App.xaml.cs l.192-198 ; garde DI `CompositionRootTests` reproduit la sous-chaîne à 7 params et résout sans exception. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Suite complète (non-régression, ~316 attendus) | `dotnet test` | Réussi : échec 0, réussite **316**, total 316 | ✓ PASS |
| Build 0 avertissement / 0 erreur | (inclus dans dotnet test) | Chronos.dll + Chronos.Tests.dll compilés proprement | ✓ PASS |
| Couche Services neutre (aucun assembly WPF, aucun HWND public) | `ServicesLayerPurityTests` | Inclus dans les 316 verts (WindowsForegroundWatch n'expose qu'un bool) | ✓ PASS |
| Garde DI réelle du graphe bureau + focus | `CompositionRootTests.Le_graphe_DI_resout_les_services_bureau_UIA` | Résolution sans exception de SessionMonitor(7) + DesktopUiaPollService | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ----------- | ----------- | ------ | -------- |
| NET-01 | 14-01 | Disparition sur « répondu » (retour Working) | ✓ SATISFIED | Tracker transition attente→non-attente + filtre treated ; 3 tests. |
| NET-02 | 14-01 (logique) / 14-02 (focus réel) | Disparition sur focus premier-plan ≥ ~2,5 s, debounce | ✓ SATISFIED | Logique testée par faux focus (4 tests) ; signal OS réel câblé (WindowsForegroundWatch en DI). Comportement OS final → vérif humaine. |
| NET-03 | 14-01 | Réapparition sur épisode d'attente plus récent + purge | ✓ SATISFIED | Horodatage d'épisode stable (non-UpdatedAt) + purge `cur > tts` ; 2 tests. |
| NET-04 | 14-01 | Archivage manuel distinct/permanent | ✓ SATISFIED | ArchiveStore sans Remove, filtré avant treated ; test de contraste. |

Aucun requirement orphelin : les 4 IDs déclarés (ROADMAP Phase 14) sont tous couverts par un plan et implémentés.

### Anti-Patterns Found

Aucun blocker. Points notés (non bloquants) :

| File | Pattern | Severity | Impact |
| ---- | ------- | -------- | ------ |
| WindowsForegroundWatch.cs | `catch { return false; }` (best-effort) | ℹ️ Info | INTENTIONNEL et conforme au CONTEXT.md (focus indisponible → NET-02 dort sans erreur, aucun faux traitement). |
| TreatedStore / SessionMonitor | `catch { }` silencieux sur I/O et Observe | ℹ️ Info | Tolérance délibérée (aucune source ≠ crash) — cohérent avec ArchiveStore et le reste du projet. |

Le stub connu du plan 01 (`IForegroundWatch` sans impl. réelle, NET-02 dormant) est RÉSOLU par le plan 02 : `WindowsForegroundWatch` est câblé en DI (App.xaml.cs l.192-198). Aucun stub résiduel.

### Robustesse / tolérance vérifiée

- **Non-régression** : `SessionMonitor` construit sans les nouveaux paramètres reste valide (test `Monitor_sans_tracker_ni_treated_ne_regresse_pas`) ; params ajoutés EN FIN, nuls par défaut.
- **Aucune source ≠ crash** : `_tracker?.Observe` et `_foreground?.IsClaudeForeground()` sous try/catch dans Read ; source bureau déjà sous try/catch.
- **Focus indisponible** : `WindowsForegroundWatch` entièrement sous try/catch → false ; NET-01 reste indépendant du focus.
- **Piège UpdatedAt évité** : la réversibilité NE repose PAS sur `treatedWaitingTs >= UpdatedAt` dans le filtre (qui serait faux pour le bureau, UpdatedAt==now à chaque poll) mais sur un horodatage d'épisode stable maintenu par le tracker — décision explicite et vérifiée dans le code (SessionMonitor l.107-109, tracker l.19-22, 77).

### Human Verification Required

1. **Acquittement par focus réel (NET-02, brin OS final)**
   - **Test :** ouvrir l'app bureau Claude sur une session en attente, la garder au premier plan (fenêtre active, titre « Claude ») ≥ ~2,5 s ; puis retenter avec un survol < 2,5 s.
   - **Attendu :** la session disparaît de la liste après ~2,5 s de focus continu ; un survol bref ne la fait pas disparaître.
   - **Pourquoi humain :** `WindowsForegroundWatch` lit le titre de la vraie fenêtre de premier plan via Win32, non testable sans fenêtre réelle. La logique d'hystérésis derrière ce signal est intégralement prouvée par faux focus.

### Gaps Summary

Aucun gap bloquant. Les 4 truths (NET-01..04) sont vérifiées : magasin réversible + tracker stateful + double filtre archived/treated en place et prouvés par 12 tests synthétiques ; focus OS réel câblé en DI ; suite complète 316/316 verte ; couche Services neutre préservée ; tolérance conforme. Le seul point renvoyé à la vérification humaine est le comportement OS terminal de NET-02 (focus de la vraie fenêtre Claude), non automatisable — sa logique amont est entièrement couverte automatiquement.

---

_Verified: 2026-07-10_
_Verifier: Claude (gsd-verifier)_
