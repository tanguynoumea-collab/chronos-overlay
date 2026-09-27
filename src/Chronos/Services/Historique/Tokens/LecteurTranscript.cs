using System.IO;
using System.Text.Json;
using Chronos.Services;

namespace Chronos.Services.Historique.Tokens;

/// <summary>Un message assistant tel que lu dans un transcript : <see cref="Id"/> = <c>message.id</c>, à défaut <c>requestId</c>,
/// null si aucun des deux (compté tel quel, jamais indexé — D-33-11). Les quatre compteurs sont ceux de la LIGNE (un bloc
/// partiel peut en porter une valeur inférieure au total du message) : c'est <c>IndexMessages</c> qui fusionne par le max.</summary>
public sealed record MessageLu(string? Id, DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR);

/// <summary>Bilan d'une lecture. <see cref="OffsetDerniereLigneComplete"/> = octets jusqu'au dernier <c>\n</c> INCLUS d'une ligne
/// traitée : c'est la valeur à persister comme curseur. <see cref="LignesIgnorees"/> = lignes pré-filtrées mais illisibles (JSON
/// invalide, timestamp absent) ; refuser une ligne <c>user</c> n'est PAS l'ignorer. <see cref="BloqueSurLigneFuture"/> : la lecture
/// s'est arrêtée devant une ligne du futur proche, le curseur ne l'a pas dépassée (D-33-09).</summary>
public sealed record ResultatLecture(
    long OffsetDerniereLigneComplete,
    int LignesLues,
    int LignesPrefiltrees,
    int LignesAssistant,
    int LignesIgnorees,
    int LignesSansTokens,
    int LignesFuturesIgnorees,
    bool BloqueSurLigneFuture,
    DateTimeOffset? PlusAncienTs,
    DateTimeOffset? PlusRecentTs,
    bool Annulee);

/// <summary>
/// TOK-02/TOK-03 — lit un transcript JSONL EN FLUX, au niveau OCTET.
///
/// POURQUOI pas le lecteur de texte ligne à ligne du framework (<c>ReadLine</c>) : son décodeur lit en avance, la position du flux sous-jacent ne correspond à aucune fin
/// de ligne, et un curseur posé là coupe une ligne (Pitfall 2). Ici : <c>FileStream</c> en partage lecture / écriture / suppression
/// (Claude Code écrit et purge pendant qu'on lit), balayage séquentiel, tampon de 64 Ko, découpe sur <c>\n</c> ; une ligne plus
/// longue que le tampon (1,36 Mo mesuré) s'accumule ; le fragment final sans <c>\n</c> n'est PAS traité et le curseur = octets
/// jusqu'au dernier <c>\n</c> inclus.
///
/// Le pré-filtre texte (la séquence d'octets type / assistant : 47 % des lignes passent, aucune fausse ligne assistant mesurée)
/// est une ÉCONOMIE ; l'autorité reste <c>type == assistant</c> ET <c>message.role == assistant</c> (motif
/// <c>TranscriptSessionSource</c>). Les quatre compteurs de usage ne sont lus QUE par <see cref="DedupUsage.LireUsage"/> (garde
/// CPT-01). <c>Sub</c> vient du dossier parent immédiat <c>subagents</c> (D-33-12).
///
/// Une ligne future de moins de <see cref="ToleranceFutur"/> ARRÊTE la lecture devant elle (latence d'écriture mesurée : +3 s) :
/// le curseur ne la dépasse pas, elle sera relue au cycle suivant ; au-delà, horloge décalée : ignorée et comptée. Une ligne
/// assistant à quatre compteurs nuls (<c>&lt;synthetic&gt;</c>) n'est ni traitée ni « ignorée » : il n'y a aucun token connu à
/// jeter (D-33-10). Mesuré : 788–800 Mo/s à cache chaud. Type NEUTRE, sans état, sans horloge propre : <c>now</c> est un paramètre.
/// </summary>
public static class LecteurTranscript
{
    public const int TailleTampon = 64 * 1024;

    /// <summary>Le jeton d'annulation est consulté AVANT la première ligne puis toutes les N lignes : rien n'est compté à moitié,
    /// et l'offset rendu est celui de la dernière ligne complète TRAITÉE.</summary>
    public const int LignesEntreControles = 4096;

    /// <summary>Au-delà de cette avance, une ligne future n'est plus une latence d'écriture mais une horloge décalée (Pitfall 3
    /// de la phase 19) : elle est ignorée et comptée, le curseur avance.</summary>
    public static readonly TimeSpan ToleranceFutur = TimeSpan.FromHours(24);

    private const string ModeleAbsent = "<absent>";

    // Le SEUL endroit du fichier où vit la séquence d'octets du pré-filtre.
    private static ReadOnlySpan<byte> MarqueurAssistant => "\"type\":\"assistant\""u8;

    /// <summary>Convention unique du dépôt : le dossier parent immédiat s'appelle <c>subagents</c> (ordinal-insensible).</summary>
    public static bool EstSousAgent(string chemin)
        => string.Equals(Path.GetFileName(Path.GetDirectoryName(chemin)), "subagents", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lit <paramref name="chemin"/> depuis l'octet <paramref name="depuis"/> (une fin de ligne connue ; au-delà de la taille du
    /// fichier → repris de 0 : le fichier a raccourci) et appelle <paramref name="traiter"/> pour chaque message assistant porteur
    /// d'au moins un token. Ne lève JAMAIS : fichier absent, verrouillé ou illisible → résultat vide à <paramref name="depuis"/>
    /// (le fichier sera retenté au cycle suivant).
    /// </summary>
    public static ResultatLecture Lire(string chemin, long depuis, DateTimeOffset now, Action<MessageLu> traiter, CancellationToken ct)
    {
        FileStream fs;
        try
        {
            fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                                bufferSize: 1, FileOptions.SequentialScan);
        }
        catch (IOException) { return Vide(depuis); }
        catch (UnauthorizedAccessException) { return Vide(depuis); }

        using (fs)
        {
            var c = new Compteurs();
            long debutLecture;
            try
            {
                debutLecture = depuis < 0 || depuis > fs.Length ? 0 : depuis;
                fs.Seek(debutLecture, SeekOrigin.Begin);
            }
            catch (IOException) { return Vide(depuis); }

            var sub = EstSousAgent(chemin);
            var offsetLigne = debutLecture;            // début de la ligne en cours
            var offsetDerniereComplete = debutLecture; // à persister : juste après le dernier '\n' d'une ligne traitée
            var tampon = new byte[TailleTampon];
            MemoryStream? reste = null;                // fragment à cheval sur deux lectures (ligne > 64 Ko ou coupée par le tampon)
            var lignesDepuisControle = 0;
            var arret = false;

            try
            {
                int n;
                while (!arret && (n = fs.Read(tampon, 0, tampon.Length)) > 0)
                {
                    var debut = 0;
                    while (debut < n)
                    {
                        var idx = Array.IndexOf(tampon, (byte)'\n', debut, n - debut);
                        if (idx < 0)
                        {
                            (reste ??= new MemoryStream()).Write(tampon, debut, n - debut);   // pas de '\n' : attendre la suite
                            break;
                        }

                        if (lignesDepuisControle == 0 && ct.IsCancellationRequested)
                        {
                            c.Annulee = true;
                            arret = true;
                            break;
                        }

                        // La ligne complète : directement sur le tampon (cas courant, aucune copie) ou jointe au reste.
                        ReadOnlyMemory<byte> ligne;
                        int longueurBrute;
                        if (reste is null || reste.Length == 0)
                        {
                            longueurBrute = idx - debut;
                            ligne = new ReadOnlyMemory<byte>(tampon, debut, longueurBrute);
                        }
                        else
                        {
                            reste.Write(tampon, debut, idx - debut);
                            var jointe = reste.ToArray();
                            reste.SetLength(0);
                            longueurBrute = jointe.Length;
                            ligne = jointe;
                        }
                        if (ligne.Length > 0 && ligne.Span[^1] == (byte)'\r') ligne = ligne[..^1];   // CRLF par prudence

                        c.LignesLues++;
                        if (ligne.Span.IndexOf(MarqueurAssistant) >= 0)
                        {
                            c.LignesPrefiltrees++;
                            if (!Traiter(ligne, now, sub, traiter, c))
                            {
                                // Ligne du futur proche : on s'arrête DEVANT elle, sans avancer le curseur (D-33-09).
                                arret = true;
                                break;
                            }
                        }

                        // Le document JSON est libéré avant ce point : le tampon peut être réutilisé (Pitfall 6).
                        offsetDerniereComplete = offsetLigne + longueurBrute + 1;   // '\n' compris
                        offsetLigne = offsetDerniereComplete;
                        debut = idx + 1;
                        lignesDepuisControle = (lignesDepuisControle + 1) % LignesEntreControles;
                    }
                }
            }
            catch (IOException)
            {
                // Lecture interrompue (fichier remplacé sous nous) : on rend ce qui a été traité, le curseur est cohérent.
            }

            return c.Vers(offsetDerniereComplete);
        }
    }

    // Rend false UNIQUEMENT pour « ligne future proche » : la lecture doit s'arrêter devant elle.
    private static bool Traiter(ReadOnlyMemory<byte> ligne, DateTimeOffset now, bool sub, Action<MessageLu> traiter, Compteurs c)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(ligne); }
        catch (JsonException) { c.LignesIgnorees++; return true; }   // ligne corrompue : ignorée et comptée (ROB-02)

        using (doc)
        {
            var o = doc.RootElement;
            if (!EstAssistant(o)) return true;   // le pré-filtre a laissé passer une ligne user / summary : ce n'est pas une erreur
            c.LignesAssistant++;

            var ts = o.TryGetProperty("timestamp", out var t) && t.ValueKind == JsonValueKind.String
                ? UsageNormalization.InstantDepuisIso(t.GetString())
                : null;
            if (ts is not { } instant) { c.LignesIgnorees++; return true; }

            if (instant > now)
            {
                if (instant - now <= ToleranceFutur) { c.BloqueSurLigneFuture = true; return false; }
                c.LignesFuturesIgnorees++;
                return true;
            }

            var model = o.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object
                        && m.TryGetProperty("model", out var md) && md.ValueKind == JsonValueKind.String
                ? md.GetString() ?? ModeleAbsent
                : ModeleAbsent;

            DedupUsage.LireUsage(o, out var messageId, out var requestId,
                                 out var entree, out var sortie, out var cacheEcrit, out var cacheLu);
            if (entree == 0 && sortie == 0 && cacheEcrit == 0 && cacheLu == 0)
            {
                c.LignesSansTokens++;   // D-33-10 : rien à compter, rien à indexer
                return true;
            }

            if (c.PlusAncienTs is null || instant < c.PlusAncienTs) c.PlusAncienTs = instant;
            if (c.PlusRecentTs is null || instant > c.PlusRecentTs) c.PlusRecentTs = instant;

            traiter(new MessageLu(messageId ?? requestId, instant, model, sub, entree, sortie, cacheEcrit, cacheLu));
            return true;
        }
    }

    // L'autorité : objet structuré, type == assistant ET message.role == assistant (jamais une chaîne dans un champ content).
    private static bool EstAssistant(JsonElement o)
    {
        if (o.ValueKind != JsonValueKind.Object) return false;
        if (!o.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String || t.GetString() != "assistant")
            return false;
        if (!o.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object)
            return false;
        return m.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() == "assistant";
    }

    private static ResultatLecture Vide(long depuis)
        => new(depuis, 0, 0, 0, 0, 0, 0, false, null, null, false);

    private sealed class Compteurs
    {
        public int LignesLues, LignesPrefiltrees, LignesAssistant, LignesIgnorees, LignesSansTokens, LignesFuturesIgnorees;
        public bool BloqueSurLigneFuture, Annulee;
        public DateTimeOffset? PlusAncienTs, PlusRecentTs;

        public ResultatLecture Vers(long offset)
            => new(offset, LignesLues, LignesPrefiltrees, LignesAssistant, LignesIgnorees, LignesSansTokens,
                   LignesFuturesIgnorees, BloqueSurLigneFuture, PlusAncienTs, PlusRecentTs, Annulee);
    }
}
