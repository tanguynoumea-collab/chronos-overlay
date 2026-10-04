using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Source de DELTA des transcripts JSONL (%USERPROFILE%\.claude\projects\**\*.jsonl), en streaming
/// tolérant. Elle ne répond qu'à DEUX questions bornées (DEL-01 / DEL-02) : y a-t-il eu une réponse
/// assistant depuis T, et combien de tokens depuis T. Elle ne produit JAMAIS de pourcentage, ni
/// aucun instantané d'usage.
///
/// POURQUOI : les limites Anthropic pondèrent par modèle (une heure d'Opus ne pèse pas comme une
/// heure de Haiku), donc le rapport d'un comptage de tokens à un plafond reste faux même avec le bon
/// plafond — c'est le principe même du calcul qui est invalide, pas son paramètre. Le comptage de
/// tokens ne peut servir que de DELTA, appuyé sur un relevé exact daté.
///
/// Le parcours disque est celui, éprouvé, de l'ancien provider d'estimation — chaque garde
/// correspond à un bug déjà rencontré et testé : écriture concurrente de Claude Code (flux ouvert en
/// partage lecture/écriture), dernière ligne tronquée et ligne corrompue ignorées (ROB-02),
/// faux positifs de prose (détection d'assistant STRUCTURÉE), horloge décalée (timestamps futurs
/// écartés), sous-dossier subagents/ inclus (même pool de quota), fichiers inertes filtrés au mtime.
///
/// Une SEULE passe disque : elle matérialise le journal, les bornages sont ensuite calculés EN
/// MÉMOIRE par <see cref="TranscriptActivityLog"/> (Pattern 2). Type NEUTRE : aucun type WPF.
/// Depuis la phase 42.3 (P-07 étape 1), cette passe est incrémentale : seuls les fichiers dont la taille ou
/// la date de modification a changé sont rouverts, les autres sont rejoués depuis un cache par fichier.
///
/// Depuis la phase 32 (CPT-01), la somme des usages passe par <see cref="DedupUsage"/> : une ligne
/// <c>assistant</c> par bloc de contenu, même <c>message.id</c>, <c>output_tokens</c> partiel croissant —
/// voir <c>docs/data-sources.md</c> §7. Cette classe ne lit plus AUCUN champ de <c>message.usage</c>.
/// </summary>
public sealed class TranscriptActivityProvider : ITranscriptActivitySource
{
    /// <summary>
    /// Profondeur d'histoire connue du journal. Un JSONL est append-only : son message le plus récent
    /// est postérieur ou égal à son mtime, donc un fichier non écrit depuis plus de 8 jours (fenêtre
    /// hebdo 7 j + marge) ne peut contenir aucun message plus récent — on l'ignore pour éviter de
    /// scanner tout l'historique. UNE seule déclaration : le filtre de scan et l'horizon annoncé à
    /// l'appelant ne doivent JAMAIS pouvoir diverger, sinon le journal mentirait sur ce qu'il couvre.
    /// </summary>
    private static readonly TimeSpan HorizonSpan = TimeSpan.FromDays(8);

    /// <summary>
    /// Précaution « fichier chaud » (42.3) : un fichier modifié il y a moins de ce délai (mesuré sur l'horloge
    /// INJECTÉE) est toujours relu en entier, même à taille et date identiques à son entrée de cache. Filet
    /// contre la granularité de la mtime, les écritures rapprochées et le décalage d'horloge du système de
    /// fichiers. Une mtime dans le futur (écart négatif) est aussi traitée comme chaude.
    /// </summary>
    private static readonly TimeSpan DelaiFichierChaud = TimeSpan.FromMinutes(2);

    // Une ligne assistant BRUTE, telle que lue sur le disque — horodatage futur compris : le filtre
    // « futur écarté » s'applique AU REJEU, car un message futur au moment de la lecture peut être passé
    // au tick suivant sans que le fichier ait changé.
    private sealed record LigneAssistant(string? MessageId, string? RequestId, DateTimeOffset When,
                                         long In, long Out, long CacheW, long CacheR);

    // Taille et mtime lues AVANT l'ouverture : un fichier qui grossit pendant la lecture aura une taille
    // différente au tick suivant et sera relu.
    private sealed record EntreeCache(long Taille, DateTime MtimeUtc, IReadOnlyList<LigneAssistant> Lignes);

    private readonly ChronosPaths _paths;
    private readonly IClock _clock;

    // P-07 étape 1 / DS-PERF-01 : cache par fichier (chemin) -> (taille, mtime, lignes assistant brutes).
    // LIMITE assumée : une réécriture du fichier à taille ET mtime identiques passerait inaperçue — c'est
    // impossible pour un JSONL append-only (toute écriture change la taille) ; filet supplémentaire, un
    // fichier modifié il y a moins de DelaiFichierChaud est toujours relu en entier.
    private readonly Dictionary<string, EntreeCache> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Le provider est déjà appelé sous le verrou de SourceActiviteMemoisee, mais le cache ne doit pas
    // dépendre de son décorateur pour rester cohérent.
    private readonly SemaphoreSlim _verrou = new(1, 1);

    /// <summary>Preuve (tests) : nombre de fichiers rouverts et relus en entier par la dernière passe.</summary>
    internal int FichiersRelusDernierePasse { get; private set; }

    /// <summary>Preuve (tests) : nombre de fichiers repris du cache, sans ouverture, par la dernière passe.</summary>
    internal int FichiersReutilisesDernierePasse { get; private set; }

    /// <summary>Preuve (tests) : nombre de fichiers détenus par le cache après la dernière passe.</summary>
    internal int FichiersEnCache => _cache.Count;

    public TranscriptActivityProvider(ChronosPaths paths, IClock clock)
    {
        _paths = paths;
        _clock = clock;
    }

    /// <summary>
    /// Passe disque : matérialise (instant, tokens) pour chaque réponse assistant récente, puis rend un
    /// journal pur interrogeable N fois. Un fichier DISPARU entre l'énumération et la lecture est ignoré ;
    /// un fichier PRÉSENT mais illisible (partage refusé, accès refusé) fait échouer la passe : un journal
    /// amputé de son activité affirmerait « aucune activité » et ne doit rien certifier (DS2-02). L'appelant
    /// (LastExactUsageProvider) convertit cet échec en « Indisponible ».
    ///
    /// Depuis 42.3 la passe est INCRÉMENTALE : seuls les fichiers dont (taille, mtime) a changé — ou encore
    /// « chauds » — sont relus ; les autres sont repris du cache. Le journal est ensuite reconstruit en
    /// REJOUANT toutes les lignes, fichier par fichier dans l'ordre d'énumération, ligne par ligne dans
    /// l'ordre du fichier, dans un <see cref="DedupUsage"/> neuf : il est donc identique, par construction,
    /// à celui d'une passe complète.
    /// </summary>
    public async Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
    {
        await _verrou.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = _clock.UtcNow;
            int relus = 0, reutilises = 0;

            // UN dictionnaire NEUF pour TOUTE la passe (D-32-03) : 491 ids sur 8 jours vivent dans 2 a 3
            // fichiers (reprise / fork de session), une dedup par fichier les compterait encore plusieurs fois.
            // Il n'est alimente qu'au rejeu ci-dessous, une fois le cache mis a jour.
            var dedup = new DedupUsage();

            // Lignes de chaque fichier énuméré, dans l'ordre d'énumération (celui d'une passe complète).
            var parFichier = new List<IReadOnlyList<LigneAssistant>>();
            var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in EnumerateJsonl(_paths.ProjectsRoot, now))
            {
                long taille;
                DateTime mtime;
                try
                {
                    var info = new FileInfo(file);
                    taille = info.Length;                                 // fichier disparu -> FileNotFoundException
                    mtime = info.LastWriteTimeUtc;
                }
                catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
                {
                    // Disparu entre l'énumération et la lecture : aucune activité à perdre -> ignoré ET oublié.
                    // Toute autre IOException / UnauthorizedAccessException remonte (DS2-02).
                    _cache.Remove(file);
                    continue;
                }

                if (_cache.TryGetValue(file, out var entree)
                    && entree.Taille == taille && entree.MtimeUtc == mtime
                    && now.UtcDateTime - mtime >= DelaiFichierChaud)
                {
                    vus.Add(file);
                    parFichier.Add(entree.Lignes);
                    reutilises++;
                    continue;
                }

                var lignes = await LireFichierAsync(file, ct).ConfigureAwait(false);
                if (lignes is null)
                {
                    _cache.Remove(file);                                  // disparu : ignoré ET oublié
                    continue;
                }

                _cache[file] = new EntreeCache(taille, mtime, lignes);
                vus.Add(file);
                parFichier.Add(lignes);
                relus++;
            }

            // Fichiers supprimés ou sortis de l'horizon : ils quittent le cache.
            foreach (var chemin in _cache.Keys.Where(k => !vus.Contains(k)).ToList())
                _cache.Remove(chemin);

            // Rejeu dans le dictionnaire NEUF de cette passe, fichier par fichier dans l'ordre d'enumeration.
            foreach (var lignes in parFichier)
            {
                foreach (var l in lignes)
                {
                    if (l.When > now) continue;                           // Pitfall 3 : timestamps futurs ecartes, AU REJEU
                    // CPT-01 : une ligne par bloc de contenu, un message compte UNE fois (max par champ).
                    dedup.Ajouter(l.MessageId, l.RequestId, l.When, l.In, l.Out, l.CacheW, l.CacheR);
                }
            }

            FichiersRelusDernierePasse = relus;
            FichiersReutilisesDernierePasse = reutilises;

            var entries = dedup.Entrees().ToList();                        // materialisation dedoublonnee
            entries.Sort((a, b) => a.Ts.CompareTo(b.Ts));                 // tri global (fichiers non tries entre eux)

            return new TranscriptActivityLog(now, now - HorizonSpan, entries);
        }
        finally { _verrou.Release(); }
    }

    // Lecture intégrale et tolérante d'un fichier : TOUTES les lignes assistant, futures comprises.
    // null si le fichier a DISPARU (FileNotFound / DirectoryNotFound) — l'appelant l'ignore et le retire du
    // cache. Un fichier PRÉSENT mais illisible (partage refusé, accès refusé) laisse remonter l'exception :
    // la passe échoue au lieu de rendre un journal amputé de l'activité de ce fichier — qui est, par
    // construction, un fichier modifié ou chaud, les autres étant servis du cache sans ouverture (DS2-02).
    private static async Task<IReadOnlyList<LigneAssistant>?> LireFichierAsync(string file, CancellationToken ct)
    {
        FileStream? fs = null;
        // FileShare.ReadWrite : Claude Code ecrit le transcript en parallele.
        try { fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException) { return null; }

        var lignes = new List<LigneAssistant>();
        await using (fs)
        using (var reader = new StreamReader(fs))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);            // ligne partielle/corrompue -> JsonException
                    var o = doc.RootElement;
                    if (!IsAssistant(o)) continue;                        // type==assistant ET message.role==assistant
                    if (!o.TryGetProperty("timestamp", out var ts)) continue;
                    if (!DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var when)) continue;

                    DedupUsage.LireUsage(o, out var messageId, out var requestId,
                                         out var input, out var output, out var cacheW, out var cacheR);
                    lignes.Add(new LigneAssistant(messageId, requestId, when, input, output, cacheW, cacheR));
                }
                catch (JsonException) { /* ligne invalide ignoree (ROB-02) */ }
            }
        }
        lignes.TrimExcess();
        return lignes;
    }

    // Enumere les *.jsonl sous root. Dossier absent / inaccessible -> sequence vide (jamais d'exception).
    private static IEnumerable<string> EnumerateJsonl(string root, DateTimeOffset now)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        try
        {
            // Recursif total : inclut INTENTIONNELLEMENT le sous-dossier subagents/ — les sous-agents
            // consomment le MEME pool de quota de compte, donc leurs tokens comptent dans la somme
            // (arbitrage phase 3). Exploitation STRUCTUREE des sous-agents = differee V2-01. AUCUN
            // filtre d'exclusion n'est pose ici.
            //
            // Perf : un JSONL est append-only -> son message le plus recent >= LastWriteTime. Un fichier
            // non ecrit depuis plus que l'horizon ne peut contenir de message plus recent, donc ne
            // contribue a aucune requete : on l'ignore pour eviter de scanner tout l'historique.
            var cutoff = now - HorizonSpan;
            return Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories)
                .Where(f => RecentEnough(f, cutoff));
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    // mtime tolerant : un fichier illisible/disparu est conserve (on le tentera puis on l'ignorera en lecture).
    private static bool RecentEnough(string file, DateTimeOffset cutoff)
    {
        try { return File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    // Objet structure uniquement : type=="assistant" ET message.role=="assistant". Ne matche JAMAIS
    // une chaine "five_hour"/"seven_day" dans un champ content (faux positifs de prose evites).
    private static bool IsAssistant(JsonElement o)
    {
        if (!o.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String
            || t.GetString() != "assistant")
            return false;
        if (!o.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object)
            return false;
        return m.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String
            && r.GetString() == "assistant";
    }
}
