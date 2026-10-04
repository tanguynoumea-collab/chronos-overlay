using System.Globalization;
using System.IO;

namespace Chronos.Services;

/// <summary>
/// MAT-3 (phase 42.2) — mise en QUARANTAINE d'un fichier propre à Chronos devenu illisible, avant qu'il ne soit réécrit.
///
/// Règle (reprise de settings.json, 42.2-02) : un original illisible n'est JAMAIS supprimé ni écrasé. Il est RENOMMÉ dans
/// le même dossier en <c>&lt;nom&gt;.illisible-AAAAMMJJ-HHMMSS[-n]&lt;ext&gt;</c> — octets identiques, retrouvable à la main —
/// et seulement ensuite le magasin réécrit un fichier sain. Une E/S passagère n'est pas « illisible » : c'est à l'appelant de
/// ne jamais appeler ceci sur un fichier qu'il n'a pas pu LIRE.
///
/// Type NEUTRE : aucun type WPF. Ne lève jamais.
/// </summary>
public static class QuarantaineFichier
{
    /// <summary>Nombre de suffixes essayés quand le nom horodaté est déjà pris (deux quarantaines dans la même seconde).</summary>
    public const int EssaisNom = 100;

    private const int EssaisMove = 3;

    /// <summary>
    /// Renomme <paramref name="chemin"/> en <c>&lt;nom&gt;.illisible-…</c> (même dossier, <c>overwrite: false</c>). Rend true
    /// et le chemin conservé dans <paramref name="cible"/> en cas de succès ; false et une cause « Type : message » sinon
    /// (fichier tenu sans partage de suppression, droits, aucun nom libre). RENOMMAGE uniquement : jamais de suppression.
    /// </summary>
    public static bool Mettre(string chemin, out string? cible, out string? cause)
    {
        cible = null;
        cause = null;
        try
        {
            var dossier = Path.GetDirectoryName(chemin) ?? ".";
            var ext = Path.GetExtension(chemin);
            var baseNom = Path.GetFileNameWithoutExtension(chemin) + ".illisible-"
                          + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            for (var n = 0; n <= EssaisNom; n++)
            {
                var candidat = Path.Combine(dossier, baseNom + (n == 0 ? "" : "-" + n) + ext);
                if (File.Exists(candidat)) continue;
                for (var essai = 1; ; essai++)
                {
                    try
                    {
                        File.Move(chemin, candidat, overwrite: false);
                        cible = candidat;
                        return true;
                    }
                    catch (IOException) when (File.Exists(candidat) && File.Exists(chemin))
                    {
                        break;   // nom pris entre-temps : suffixe suivant
                    }
                    catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && essai < EssaisMove)
                    {
                        Thread.Sleep(10);   // antivirus, indexeur : reprise courte
                    }
                }
            }
            cause = "aucun nom libre pour " + baseNom + ext;
            return false;
        }
        catch (Exception ex)
        {
            cause = ex.GetType().Name + " : " + ex.Message;
            return false;
        }
    }
}
