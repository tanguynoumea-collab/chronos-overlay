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
/// 32-06 ajoutera ici <c>Lire(dossier, de, a)</c> (mois UTC chevauchant une plage).</para>
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
}
