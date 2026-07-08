# Phase 7: Packaging + déploiement - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

L'application se distribue en un exécutable unique autonome et fonctionne sur une machine propre.
Requirement couvert : DEP-01.
</domain>

<decisions>
## Implementation Decisions

### Config de publication (VERROUILLÉE — .planning/research/STACK.md)
- `dotnet publish -c Release -r win-x64` avec : PublishSingleFile=true, SelfContained=true (explicite),
  IncludeNativeLibrariesForSelfExtract=true, EnableCompressionInSingleFile=true, PublishTrimmed=false
  (WPF non trim-safe — NON NÉGOCIABLE), PublishReadyToRun=true (démarrage autostart plus rapide),
  InvariantGlobalization=false (formatage fr-FR).
- Les propriétés de publish sont déjà conditionnées dans Chronos.csproj (Phase 1) — vérifier qu'elles
  s'activent bien au publish et pas au build debug.
- Sortie attendue : un seul Chronos.exe (± fichiers .pdb à exclure) dans bin/Release/net8.0-windows/win-x64/publish/.

### Vérifications requises (success criteria)
1. Publication OK, exe unique, taille raisonnable (~60-70 Mo compressé attendu).
2. Lancement de l'EXE PUBLIÉ (pas le build debug) : démarre, cadran visible, pas de crash — l'extraction
   des natives au 1er run fonctionne. « Machine propre sans runtime .NET » : non testable littéralement ici,
   approximer par le fait que l'exe est self-contained (aucune dépendance au runtime installé) + lancement réel réussi.
3. Autostart : le .lnk pointe vers Environment.ProcessPath — vérifier que le toggle autostart depuis l'exe
   publié crée un raccourci pointant vers l'exe publié (chemin stable après déplacement = le raccourci suit
   l'exe qui l'a créé ; documenter la limite : si l'utilisateur déplace l'exe, il doit re-toggler).

### Livrables annexes
- Script/documentation de publication (docs/publish.md ou section README) avec la commande exacte.
- Optionnel : profil de publication Properties/PublishProfiles/win-x64.pubxml.

### Claude's Discretion
Nom du profil de publication, emplacement de la doc, éventuel script .cmd/.ps1 de publication.
</decisions>

<code_context>
## Existing Code Insights

- Chronos.csproj : propriétés publish conditionnées depuis la Phase 1 (à re-vérifier).
- AutostartService : cible Environment.ProcessPath (single-file-safe, décidé Phase 6).
- 106 tests verts — la publication ne doit rien changer au code.
- ChronosPaths/AppContext.BaseDirectory déjà utilisés (pas d'Assembly.Location) — compatible mono-fichier.
</code_context>

<specifics>
## Specific Ideas

- Tester l'exe publié AVANT de le déclarer bon : lancement réel ~8 s, fenêtre visible, kill propre.
- Vérifier qu'aucun fichier annexe requis ne traîne à côté de l'exe publié (hors .pdb).
</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.
</deferred>
