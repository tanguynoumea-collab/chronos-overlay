using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Chronos.Services;

/// <summary>
/// FIAB-4 / FIAB-1 (phase 42.2) — LE journal d'incidents de Chronos : des lignes « [incident] date — message » ajoutées à
/// <c>%APPDATA%\Chronos\chronos.log</c>, et reportées en tête du journal de démarrage suivant.
///
/// Pourquoi : <c>chronos.log</c> est réécrit en entier à chaque lancement (rapport de diagnostic). La ligne « arrêt dépassé »
/// ajoutée à la fermeture — la seule trace du processus zombie de quick 260927 — disparaissait donc au relancement, c'est-à-dire
/// au moment précis où l'on irait la chercher. Désormais, tout incident (arrêt dépassé, exception non gérée, démarrage raté)
/// passe par ici, et <see cref="ComposerJournalDemarrage"/> le recopie dans le nouveau journal (au plus <see cref="Conserves"/>).
///
/// Type NEUTRE : aucun type WPF. Signaler ne lève jamais : signaler une panne ne doit pas en produire une seconde.
/// </summary>
public static class JournalIncidents
{
    /// <summary>Nom du fichier journal, sous le dossier des réglages (%APPDATA%\Chronos).</summary>
    public const string NomFichier = "chronos.log";

    /// <summary>Marqueur de tête d'une ligne d'incident : c'est lui qui permet de la retrouver au démarrage suivant.</summary>
    public const string Marqueur = "[incident]";

    /// <summary>Nombre maximal d'incidents reportés d'un lancement à l'autre (les plus RÉCENTS) : le journal ne grossit pas sans fin.</summary>
    public const int Conserves = 50;

    /// <summary>Longueur maximale d'un message : une pile démesurée ne doit pas faire du journal un fichier illisible.</summary>
    public const int LongueurMax = 2000;

    // UTF-8 SANS BOM : le journal est relu en texte par ComposerJournalDemarrage et ouvert dans un éditeur ; un BOM au milieu
    // d'un fichier déjà écrit serait un caractère parasite.
    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);

    // Anciennes lignes 3.4 de l'arrêt dépassé (« [2026-09-27 10:00:00 +11:00] arrêt dépassé : … ») : elles précèdent le
    // marqueur et doivent survivre elles aussi au premier démarrage de la 3.5.
    private static readonly Regex AncienArret = new(@"^\[\d{4}-\d{2}-\d{2} [^\]]*\] arrêt dépassé", RegexOptions.CultureInvariant);

    /// <summary>
    /// Ligne d'incident MONOLIGNE : « [incident] yyyy-MM-dd HH:mm:ss zzz — message ». Les sauts de ligne du message deviennent
    /// « ⏎ » (une ligne = un incident, sinon l'extraction perdrait la suite) ; message tronqué à <see cref="LongueurMax"/>.
    /// </summary>
    public static string Ligne(DateTimeOffset quand, string message)
    {
        var texte = (message ?? "").Replace("\r\n", " ⏎ ").Replace("\n", " ⏎ ").Replace("\r", " ⏎ ");
        if (texte.Length > LongueurMax) texte = texte[..LongueurMax];
        return $"{Marqueur} {quand.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)} — {texte}";
    }

    /// <summary>
    /// Ajoute une ligne d'incident à <c>chronos.log</c> sous <paramref name="dossier"/> (créé s'il manque : plus aucune
    /// surveillance de fichier ne le crée au démarrage). Rend true si la ligne est écrite ; false sinon, SANS JAMAIS LEVER
    /// (dossier null, chemin invalide, fichier tenu, disque plein…). Trois essais rapprochés sur IOException : le journal de
    /// démarrage ou un antivirus peut tenir le fichier un instant.
    /// </summary>
    /// <para>FIAB-R3 (42.2-11) : chaque signalement passe par le <see cref="LimiteurIncidents"/> du dossier (un par journal et par
    /// processus) — <paramref name="cle"/> (par défaut : le message) dédoublonne ; au-delà de 3 occurrences, une ligne « répété
    /// N fois » toutes les 10 minutes au plus ; 200 lignes au plus par processus. Une occurrence tue rend false.</para>
    public static bool Signaler(string? dossier, string message, DateTimeOffset? quand = null, string? cle = null)
    {
        if (string.IsNullOrWhiteSpace(dossier)) return false;
        try
        {
            var instant = quand ?? DateTimeOffset.Now;
            var (decision, repetitions) = LimiteurDe(dossier).Evaluer(cle ?? message ?? "", instant);
            var texte = decision switch
            {
                DecisionIncident.Ecrire => message,
                DecisionIncident.Resume => "(répété " + repetitions.ToString(CultureInfo.InvariantCulture) + " fois depuis le dernier relevé) " + message,
                DecisionIncident.Plafond => "plafond de " + LimiteurIncidents.PlafondLignes.ToString(CultureInfo.InvariantCulture)
                                            + " incidents atteint pour ce processus : les suivants ne sont plus écrits",
                _ => null,
            };
            if (texte is null) return false;
            var ligne = Ligne(instant, texte);
            for (var essai = 1; ; essai++)
            {
                try
                {
                    Directory.CreateDirectory(dossier);
                    var chemin = Path.Combine(dossier, NomFichier);
                    // Un journal qui ne finit pas par un saut de ligne collerait l'incident à sa dernière ligne : l'extraction,
                    // qui ne lit que les débuts de ligne, le perdrait.
                    var prefixe = FinitSansSautDeLigne(chemin) ? Environment.NewLine : "";
                    File.AppendAllText(chemin, prefixe + ligne + Environment.NewLine, Utf8SansBom);
                    return true;
                }
                catch (IOException) when (essai < 3)
                {
                    Thread.Sleep(5);
                }
            }
        }
        catch
        {
            return false;   // signaler ne doit jamais produire une seconde panne
        }
    }

    /// <summary>
    /// Les lignes d'incident d'un ancien journal, dans l'ordre : celles qui commencent (blancs de tête ignorés) par
    /// <see cref="Marqueur"/>, et les anciennes lignes datées « arrêt dépassé » de la 3.4. Au plus <see cref="Conserves"/>,
    /// les DERNIÈRES. Null ou vide → liste vide.
    /// </summary>
    public static IReadOnlyList<string> Extraire(string? contenu)
    {
        if (string.IsNullOrEmpty(contenu)) return Array.Empty<string>();

        var incidents = new List<string>();
        foreach (var brute in contenu.Split('\n'))
        {
            var ligne = brute.Trim();
            if (ligne.StartsWith(Marqueur, StringComparison.Ordinal) || AncienArret.IsMatch(ligne))
                incidents.Add(ligne);
        }

        return incidents.Count <= Conserves ? incidents : incidents.GetRange(incidents.Count - Conserves, Conserves);
    }

    /// <summary>
    /// Contenu du journal de démarrage : l'en-tête, puis (s'il y en a) la section « [Incidents des lancements précédents] »
    /// recopiée de <paramref name="ancienContenu"/>, puis le rapport. Sans incident, aucune section : le journal reste
    /// identique à celui d'avant la phase 42.2.
    /// </summary>
    public static string ComposerJournalDemarrage(string? ancienContenu, string rapport)
    {
        var sb = new StringBuilder("(log automatique au démarrage)\n");
        var incidents = Extraire(ancienContenu);
        if (incidents.Count > 0)
        {
            sb.Append("[Incidents des lancements précédents]\n");
            sb.Append(string.Join("\n", incidents));
            sb.Append("\n\n");
        }
        sb.Append(rapport);
        return sb.ToString();
    }

    /// <summary>FIAB-R3 : taille maximale relue à la fin de chronos.log au démarrage (1 Mo).</summary>
    public const int LectureMaxOctets = 1024 * 1024;

    /// <summary>
    /// FIAB-R3 (42.2-11) — la FIN de <paramref name="chemin"/> : au plus <see cref="LectureMaxOctets"/> octets, la première ligne
    /// (coupée) écartée quand le fichier est plus long. Fichier absent → null. Partage lecture/écriture : un incident peut être
    /// ajouté pendant la lecture. Lève sur une E/S en échec (l'appelant décide : best-effort au démarrage).
    /// </summary>
    public static string? LireFin(string chemin, int maxOctets = LectureMaxOctets)
    {
        if (!File.Exists(chemin)) return null;
        using var flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var tronque = flux.Length > maxOctets;
        if (tronque) flux.Seek(-maxOctets, SeekOrigin.End);
        var octets = new byte[tronque ? maxOctets : flux.Length];
        var lus = 0;
        while (lus < octets.Length)
        {
            var n = flux.Read(octets, lus, octets.Length - lus);
            if (n == 0) break;
            lus += n;
        }
        var texte = Utf8SansBom.GetString(octets, 0, lus);
        if (!tronque) return texte;
        var saut = texte.IndexOf('\n');
        return saut >= 0 ? texte[(saut + 1)..] : "";
    }

    // FIAB-R3 : un limiteur par journal (dossier normalisé) et par processus.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, LimiteurIncidents> Limiteurs =
        new(StringComparer.OrdinalIgnoreCase);

    private static LimiteurIncidents LimiteurDe(string dossier)
    {
        string cle;
        try { cle = Path.GetFullPath(dossier); }
        catch { cle = dossier; }
        return Limiteurs.GetOrAdd(cle, _ => new LimiteurIncidents());
    }

    /// <summary>Vrai si le fichier existe, n'est pas vide et ne finit pas par '\n'. Faux si absent ou illisible.</summary>
    private static bool FinitSansSautDeLigne(string chemin)
    {
        if (!File.Exists(chemin)) return false;
        using var flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (flux.Length == 0) return false;
        flux.Seek(-1, SeekOrigin.End);
        return flux.ReadByte() != '\n';
    }
}
