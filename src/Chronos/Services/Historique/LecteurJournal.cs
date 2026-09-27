using System.IO;
using System.Text;
using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// Ce qu'une lecture a RÉELLEMENT tiré d'un fichier de journal : les relevés et événements lisibles, dans
/// l'ordre du fichier, et le NOMBRE de lignes sautées — un compteur exposé, jamais un silence (motif
/// <c>BilanBalayage</c> : on annonce ce qu'on a fait, pas ce qu'on aurait voulu).
/// </summary>
public sealed record LectureFichier(
    IReadOnlyList<ReleveJournal> Releves,
    IReadOnlyList<EvenementJournal> Evenements,
    int LignesIgnorees)
{
    /// <summary>Lecture d'un fichier absent ou inaccessible : rien, et rien à compter.</summary>
    public static readonly LectureFichier Vide = new(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0);
}

/// <summary>
/// JRN-03 — lecture TOLÉRANTE d'un fichier mensuel du journal, ligne par ligne.
///
/// <para>Même discipline que <c>TranscriptActivityProvider.ReadAsync</c> : partage large à l'ouverture
/// (un écrivain peut tenir le fichier), ligne vide sautée SANS être comptée (une fin de fichier après
/// « \n » n'est pas une erreur), toute ligne refusée par <see cref="LigneJournal.Parser"/> comptée dans
/// <see cref="LectureFichier.LignesIgnorees"/>. Une dernière ligne tronquée (processus tué en pleine
/// écriture) est une ligne perdue, pas un fichier perdu.</para>
///
/// <para>Ne lève JAMAIS : fichier absent → <see cref="LectureFichier.Vide"/> ; verrou tenu par un écrivain
/// (<c>FileShare.None</c> pendant quelques microsecondes) → quelques reprises courtes, puis vide. Type NEUTRE.
/// <see cref="Lire"/> (JRN-05) lit une PLAGE : les seuls mois UTC qui la chevauchent, triés et filtrés.</para>
/// </summary>
public static class LecteurJournal
{
    // Un écrivain tient le fichier le temps d'une relecture de queue et d'une écriture (< 1 ms) : céder la
    // main quelques fois suffit. Au-delà, on rend vide plutôt que d'attendre — le lecteur n'est jamais bloquant.
    private const int EssaisOuverture = 20;

    /// <summary>Lit un fichier de journal. Fichier absent ou inaccessible → lecture vide, sans lever.</summary>
    public static LectureFichier LireFichier(string chemin)
    {
        if (!File.Exists(chemin)) return LectureFichier.Vide;

        FileStream? flux = null;
        for (var essai = 1; flux is null; essai++)
        {
            try
            {
                flux = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException) { return LectureFichier.Vide; }
            catch (DirectoryNotFoundException) { return LectureFichier.Vide; }
            catch (IOException) when (essai < EssaisOuverture) { if (essai <= 12) Thread.Yield(); else Thread.Sleep(1); }
            catch (IOException) { return LectureFichier.Vide; }
            catch (UnauthorizedAccessException) { return LectureFichier.Vide; }
        }

        var releves = new List<ReleveJournal>();
        var evenements = new List<EvenementJournal>();
        var ignorees = 0;

        using (flux)
        using (var lecteur = new StreamReader(flux, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true))
        {
            string? ligne;
            while ((ligne = LireLigne(lecteur)) is not null)
            {
                ligne = ligne.TrimEnd('\r');   // un fichier repassé par un éditeur Windows ne devient pas illisible
                if (ligne.Length == 0) continue;   // ni lue, ni comptée

                if (LigneJournal.Parser(ligne, out var releve, out var evenement))
                {
                    if (releve is not null) releves.Add(releve);
                    else if (evenement is not null) evenements.Add(evenement);
                }
                else
                {
                    ignorees++;
                }
            }
        }

        return new LectureFichier(releves, evenements, ignorees);
    }

    // Une E/S qui casse en cours de lecture (fichier supprimé sous nous, disque retiré) termine la lecture
    // sur ce qui a été lu : le journal ne fait jamais tomber son lecteur.
    private static string? LireLigne(StreamReader lecteur)
    {
        try { return lecteur.ReadLine(); }
        catch (IOException) { return null; }
    }

    // --- JRN-05 : lecture par plage ---

    // Les seuls fichiers qu'une lecture par plage accepte d'ouvrir : le motif de JournalReleves.NomFichier.
    private static readonly System.Text.RegularExpressions.Regex NomMensuel =
        new(@"^releves-\d{4}-\d{2}\.jsonl$", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Lit la plage <c>[de, a[</c> : ouvre les SEULS fichiers mensuels (mois UTC, D-32-16) qui la chevauchent, rend
    /// relevés et événements filtrés et triés par <c>t</c> (tri stable), la somme des lignes ignorées, et
    /// <see cref="LectureJournal.JournalOuvertLe"/> (première ligne valide du plus ancien fichier du dossier, quelle que
    /// soit la plage). Dossier absent → lecture vide. Ne lève jamais : une E/S qui casse rend ce qui a été lu.
    /// </summary>
    public static LectureJournal Lire(string dossier, DateTimeOffset de, DateTimeOffset a)
    {
        var plage = new Plage(de, a);
        if (string.IsNullOrEmpty(dossier) || !Directory.Exists(dossier))
            return new LectureJournal(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0, null, plage);

        var releves = new List<ReleveJournal>();
        var evenements = new List<EvenementJournal>();
        var ignorees = 0;

        try
        {
            foreach (var mois in MoisUtcChevauchant(de, a))
            {
                var lecture = LireFichier(Path.Combine(dossier, JournalReleves.NomFichier(mois)));
                releves.AddRange(lecture.Releves.Where(r => plage.Contient(r.T)));
                evenements.AddRange(lecture.Evenements.Where(e => plage.Contient(e.T)));
                ignorees += lecture.LignesIgnorees;
            }
        }
        catch (IOException) { /* lecture partielle : le journal ne fait jamais tomber son lecteur */ }
        catch (UnauthorizedAccessException) { }

        return new LectureJournal(
            releves.OrderBy(r => r.T).ToList(),       // OrderBy est STABLE : deux sources au même t gardent l'ordre du fichier
            evenements.OrderBy(e => e.T).ToList(),
            ignorees,
            PremiereLigneValide(dossier),
            plage);
    }

    // Le premier jour (UTC) du mois de `de`, puis chaque mois jusqu'à celui de `a − 1 tick` inclus : la borne `a`
    // étant EXCLUE, une plage qui finit pile au 1er du mois n'ouvre pas ce mois. Plage vide ou inversée → rien.
    private static IEnumerable<DateTimeOffset> MoisUtcChevauchant(DateTimeOffset de, DateTimeOffset a)
    {
        if (a <= de) yield break;
        var dernierInstant = (a - TimeSpan.FromTicks(1)).UtcDateTime;
        var dernierMois = new DateTime(dernierInstant.Year, dernierInstant.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var premier = de.UtcDateTime;
        for (var mois = new DateTime(premier.Year, premier.Month, 1, 0, 0, 0, DateTimeKind.Utc); mois <= dernierMois; mois = mois.AddMonths(1))
            yield return new DateTimeOffset(mois);
    }

    /// <summary>
    /// ACC-01 (35-02) — « journal ouvert le … » sans lire une plage : le t de la première ligne valide (relevé ou événement)
    /// du plus ancien fichier mensuel qui en porte une. Tolérant : dossier absent, illisible ou vide → <c>null</c> (inconnu,
    /// jamais inventé), aucune exception. Lecture FICHIER PAR FICHIER qui s'arrête au premier lisible : à appeler hors du
    /// thread UI (<see cref="JournalReleves.AmorcerJournalOuvertLe"/>).
    /// </summary>
    public static DateTimeOffset? JournalOuvertLe(string dossier) => PremiereLigneValide(dossier);

    // « Journal ouvert le … » : les fichiers conformes triés par nom (ordinal = chronologique pour AAAA-MM), et dans
    // le plus ancien qui porte une ligne lisible, le t de la PREMIÈRE ligne valide (relevé ou événement). Un fichier
    // ancien entièrement illisible ne cache pas l'ouverture : on passe au suivant. Aucun → null.
    private static DateTimeOffset? PremiereLigneValide(string dossier)
    {
        string[] fichiers;
        try
        {
            fichiers = Directory.GetFiles(dossier)
                .Where(f => NomMensuel.IsMatch(Path.GetFileName(f)))
                .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                .ToArray();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        foreach (var fichier in fichiers)
        {
            var lecture = LireFichier(fichier);
            var premierReleve = lecture.Releves.Count > 0 ? lecture.Releves[0].T : (DateTimeOffset?)null;
            var premierEvenement = lecture.Evenements.Count > 0 ? lecture.Evenements[0].T : (DateTimeOffset?)null;
            var premier = Min(premierReleve, premierEvenement);
            if (premier is not null) return premier;
        }
        return null;
    }

    private static DateTimeOffset? Min(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : (a <= b ? a : b);
}
