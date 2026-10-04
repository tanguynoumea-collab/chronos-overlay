# Points d'audit externe — Chronos

Les 5 points sur lesquels l'audit externe (dev-senior, lecture seule) juge le projet **avant chaque publication**.
Validés par l'utilisateur le 2026-10-04. Toute modification est une décision explicite.

1. **Honnêteté des chiffres** — aucun chemin n'affiche une estimation comme un chiffre exact (doctrine « exact ou rien ») :
   seul le plancher « ≥ » n'est pas exact ; tokens jamais convertis en pourcentage ; trous jamais interpolés ; resets
   toujours ceux du serveur.
   *Hypothèse assumée (décision du 2026-10-04, ajoutée le 2026-10-04) :* pendant une panne des sources vivantes (sonde
   et secours OAuth), un relevé vieilli sans activité Claude Code depuis sa capture reste affiché comme exact
   (« encore valide ») ; Chronos suppose alors que Claude Code est le seul consommateur du quota. L'usage de l'app
   bureau, de Cowork et de claude.ai, qui partagent le même pool, est invisible aux transcripts. L'audit juge ce point
   à l'aune de cette hypothèse écrite (`docs/data-sources.md` §4).
2. **Intégrité des données** — aucune perte silencieuse : réglages, archives, journal des relevés et agrégats de tokens
   de Chronos ; `~/.claude/settings.json` de Claude Code (sauvegarde avant écriture, jamais réécrit s'il est illisible).
3. **Robustesse de l'overlay** — pas de plantage, pas de processus zombie, une seule instance, thread UI jamais bloqué,
   incidents journalisés dans `chronos.log`.
4. **Reprenabilité** — la chaîne de données (`docs/data-sources.md`) et le code se comprennent en 10 minutes par un
   nouveau développeur ; pas de reliquat de méthodes abandonnées.
5. **Prêt à distribuer** — build reproductible, version embarquée et cohérente, mise à jour fiable (hooks et autostart
   repointés), exe autonome, documentation d'installation exacte (README, `docs/publish.md`).
