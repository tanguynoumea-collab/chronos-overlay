---
phase: 18
slug: source-exacte-par-en-t-tes-de-rate-limit
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-12
---

# Phase 18 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~RateLimit\|FullyQualifiedName~Header\|FullyQualifiedName~Unite"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | ~55 s · baseline mesurée : **505 tests / 0 échec** |

## Sampling Rate

- Après chaque commit de tâche : commande rapide
- Après chaque vague : suite complète
- Avant vérification de phase : suite complète verte

## Contraintes de sécurité qui priment sur toute vérification

- **Aucune requête réseau réelle depuis un test.** Tout par `FakeHttpMessageHandler`.
- **Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL** de `%APPDATA%\Chronos\oauth.dat`.
  Contrôle avant/après chaque plan : **518 octets, mtime 1783863147**.
- Le jeton ne vit qu'en variable locale. Jamais logué, écrit, mis en exception, ni concaténé dans une URL.
- Le corps de la réponse HTTP n'est jamais transporté ni journalisé.
- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` — chemins depuis `Path.GetTempPath()`.

## Per-Task Verification Map

*Rempli par le planner.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| — | — | — | — | — | — | — | ⬜ pending |

## Gardes permanentes

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté Services | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert |
| Composition DI | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~CompositionRootTests"` | vert (la sonde se résout, en `primary`) |
| `CompositeUsageProvider` intact | `git diff --name-only HEAD~N -- src/Chronos/Services/CompositeUsageProvider.cs` | vide (refonte = phase 19) |
| Un seul rafraîchisseur | `grep -rln "RefreshAsync" src/Chronos` | exactement 2 fichiers (client + autorité) |
| Coffre intact | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec |

## Wave 0 Requirements

L'infrastructure xUnit existe. Wave 0 pose les briques manquantes identifiées par la recherche :

- [ ] Faux de transport capable de porter des **en-têtes de réponse sur un code d'erreur** (notamment 429) —
      `FakeHttpMessageHandler` doit le permettre ; à étendre si ce n'est pas le cas.
- [ ] Jeux d'en-têtes de référence : nominal (`allowed`), avertissement (`allowed_warning`), refus
      (`rejected`), statut **inconnu** (→ `NonReconnu`, jamais rangé d'autorité dans « autorisé »), forme
      **overage** alternative, en-têtes **absents**, en-têtes **illisibles**.
- [ ] **Test de garde culture** : la conversion doit réussir sous une culture où le séparateur décimal est
      la virgule. Vérifié empiriquement : sous fr-FR, `double.TryParse("0.63")` renvoie **false**.
      `InvariantGlobalization` est verrouillé à `false` dans ce projet (UI fr-FR), donc le piège est réel.
- [ ] **Test de garde d'unités** : garantir mécaniquement qu'aucun futur provider ne réintroduise une
      conversion divergente. La recherche a recensé **5** conversions d'unité à rapatrier en un point unique,
      pas 3.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Un 429 **réel** porte bien les en-têtes | HDR-02 | Non vérifiable sans jeton valide ET compte saturé. Le jeton de l'utilisateur est expiré depuis le 2026-07-12 | Après reconnexion, saturer la fenêtre 5 h puis consulter le diagnostic : les chiffres doivent rester exacts pendant le refus |
| Noms d'en-têtes réellement présents | HDR-01 / HDR-05 | Les en-têtes `unified` sont absents de la doc publique Anthropic (source : code de `claude-session-browser` + un dump indépendant) | Le diagnostic doit lister les **noms** d'en-têtes de limite reçus (jamais les valeurs) — tranchera au premier rafraîchissement réussi |
| Coût réel de la sonde | HDR-06 | Dépend de la facturation observée sur le compte | Vérifier l'ordre de grandeur annoncé dans les réglages après quelques heures de fonctionnement |

**HDR-02 ne doit pas être coché « prouvé en production » avant la vérification manuelle ci-dessus.**

## Validation Sign-Off

- [ ] Toutes les tâches ont une commande automatisée ou une dépendance Wave 0
- [ ] Aucune requête réseau réelle dans aucun test
- [ ] Le cas « 429 sans en-têtes » est traité explicitement, pas supposé impossible
- [ ] Un statut serveur inconnu n'est jamais rangé d'autorité dans « autorisé »
