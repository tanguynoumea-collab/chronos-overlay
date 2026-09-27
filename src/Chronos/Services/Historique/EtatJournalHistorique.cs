using System.IO;
using System.Text.RegularExpressions;
using Chronos.Models.Historique;
using Chronos.Services.Historique.Tokens;

namespace Chronos.Services.Historique;

/// <summary>Un fichier du dossier d'historique, tel que le disque le décrit : nom, taille en octets, date de modification (UTC).</summary>
public sealed record FichierHistorique(string Nom, long Taille, DateTimeOffset ModifieLe);

/// <summary>
/// ACC-03 — ce que le dossier d'historique contient, pour le diagnostic ; même lecteur que la fenêtre.
///
/// <para>Deux questions, rien de plus : quels fichiers le dossier porte (par familles : relevés, agrégats de tokens, index des
/// ids, curseurs, couverture — avec taille et date de modification), et quels sont les derniers événements du journal (relus par
/// <see cref="LecteurJournal"/>, le lecteur de la fenêtre, jamais par un second chemin). Le diagnostic ne lit pas le journal
/// lui-même : il passe par ici pour les événements et par <see cref="SourceHistoriqueDisque"/> pour la journée.</para>
///
/// <para>Ne lève JAMAIS et ne crée JAMAIS le dossier : un dossier absent ou illisible rend une liste vide. Type NEUTRE (aucun WPF),
/// aucun fuseau : les instants restent en UTC, le diagnostic les convertit avec le fuseau qu'on lui injecte.</para>
/// </summary>
public static class EtatJournalHistorique
{
    // Fenêtre des « événements récents » : une semaine, la durée d'une fenêtre hebdomadaire du forfait.
    private static readonly TimeSpan SeptJours = TimeSpan.FromDays(7);

    // Familles, dans l'ordre du rapport : le journal d'abord, puis ce que la reconstruction des tokens écrit.
    private static readonly Regex MotifReleves = new(@"^releves-\d{4}-\d{2}\.jsonl$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MotifTokens = new(@"^tokens-\d{4}-\d{2}\.jsonl$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MotifIds = new(@"^ids-.*\.jsonl$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Les fichiers d'historique du dossier, par familles — <c>releves-*</c>, <c>tokens-*</c>, <c>ids-*</c> (nom croissant dans
    /// chaque famille, donc chronologique pour AAAA-MM), puis <c>curseurs.json</c> et <c>couverture.json</c>. Tout autre fichier
    /// est ignoré. Dossier absent ou illisible → liste vide.
    /// </summary>
    public static IReadOnlyList<FichierHistorique> Inventaire(string dossier)
    {
        if (string.IsNullOrEmpty(dossier) || !Directory.Exists(dossier)) return [];
        try
        {
            return new DirectoryInfo(dossier).EnumerateFiles()
                .Select(fi => (Rang: Rang(fi.Name), Fichier: fi))
                .Where(x => x.Rang >= 0)
                .OrderBy(x => x.Rang)
                .ThenBy(x => x.Fichier.Name, StringComparer.Ordinal)
                .Select(x => new FichierHistorique(x.Fichier.Name, x.Fichier.Length, new DateTimeOffset(x.Fichier.LastWriteTimeUtc, TimeSpan.Zero)))
                .ToList();
        }
        catch (Exception)
        {
            return [];   // E/S qui casse en cours d'énumération : le diagnostic dira « Fichiers : 0 », jamais une exception
        }
    }

    /// <summary>
    /// Les <paramref name="n"/> derniers événements du journal sur <c>[now − 7 j, now]</c>, dans l'ordre chronologique — relus par
    /// <see cref="LecteurJournal"/> (le lecteur de la fenêtre). Dossier absent ou panne → liste vide.
    /// </summary>
    public static IReadOnlyList<EvenementJournal> DerniersEvenements(string dossier, DateTimeOffset now, int n = 5)
    {
        try
        {
            return LecteurJournal.Lire(dossier, now - SeptJours, now.AddTicks(1)).Evenements.TakeLast(n).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static int Rang(string nom)
    {
        if (MotifReleves.IsMatch(nom)) return 0;
        if (MotifTokens.IsMatch(nom)) return 1;
        if (MotifIds.IsMatch(nom)) return 2;
        if (nom == Curseurs.NomFichier) return 3;
        if (nom == CouvertureTokens.NomFichier) return 4;
        return -1;
    }
}
