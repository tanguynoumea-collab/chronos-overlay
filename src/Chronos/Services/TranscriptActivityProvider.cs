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
/// heure de Haiku), donc « tokens / plafond » reste faux même avec le bon plafond. Le comptage de
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

    private readonly ChronosPaths _paths;
    private readonly IClock _clock;

    public TranscriptActivityProvider(ChronosPaths paths, IClock clock)
    {
        _paths = paths;
        _clock = clock;
    }

    /// <summary>
    /// Passe disque unique : matérialise (instant, tokens) pour chaque réponse assistant récente,
    /// puis rend un journal pur interrogeable N fois. Ne lève jamais sur une source défaillante.
    /// </summary>
    public async Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var entries = new List<(DateTimeOffset Ts, long Tokens)>();

        foreach (var file in EnumerateJsonl(_paths.ProjectsRoot, now))
        {
            FileStream? fs = null;
            // FileShare.ReadWrite : Claude Code ecrit le transcript en parallele.
            try { fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
            catch (IOException) { continue; }
            await using (fs)
            using (var reader = new StreamReader(fs))
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (line.Length == 0) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);        // ligne partielle/corrompue -> JsonException
                        var o = doc.RootElement;
                        if (!IsAssistant(o)) continue;                    // type==assistant ET message.role==assistant
                        if (!o.TryGetProperty("timestamp", out var ts)) continue;
                        if (!DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind, out var when)) continue;

                        long tokens = SumUsageTokens(o);                  // input+output+cache_creation+cache_read
                        if (when <= now) entries.Add((when, tokens));     // Pitfall 3 : filtrer les timestamps futurs (horloge decalee)
                    }
                    catch (JsonException) { /* ligne invalide ignoree (ROB-02) */ }
                }
            }
        }

        entries.Sort((a, b) => a.Ts.CompareTo(b.Ts));                     // tri global (fichiers non tries entre eux)

        return new TranscriptActivityLog(now, now - HorizonSpan, entries);
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

    // Somme message.usage : input + output + cache_creation + cache_read. Chaque champ optionnel (defaut 0).
    private static long SumUsageTokens(JsonElement o)
    {
        if (!o.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object
            || !m.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object)
            return 0;

        return Field(u, "input_tokens") + Field(u, "output_tokens")
             + Field(u, "cache_creation_input_tokens") + Field(u, "cache_read_input_tokens");
    }

    private static long Field(JsonElement usage, string name)
        => usage.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
           && v.TryGetInt64(out var n) ? n : 0;
}
