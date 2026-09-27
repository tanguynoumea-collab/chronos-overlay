using System.IO;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Services.Historique;

namespace Chronos.Tests;

/// <summary>
/// JRN-05 / D-32-30 — la journée NOMINALE de 288 relevés est FABRIQUÉE par un vrai <see cref="JournalReleves"/>
/// (elle exerce aussi l'écrivain) plutôt que commitée : 288 lignes de fixture n'apprendraient rien de plus
/// qu'un helper de vingt lignes, et le helper ne peut pas dériver du format d'écriture.
/// Écrit toujours dans un dossier temporaire neuf, jamais sous %APPDATA%.
/// </summary>
internal static class FabriqueJournal
{
    private static readonly TimeSpan Cadence = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Fenetre5h = TimeSpan.FromHours(5);
    private const int RelevesParJour = 288;          // 24 h / 5 min
    private const int TranchesParFenetre = 60;       // 5 h / 5 min : r5 change toutes les 60 tranches

    /// <summary>Un dossier temporaire vide et unique, sous le dossier temp de l'utilisateur.</summary>
    public static string DossierTemp()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "chronos-tests", "journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        return dossier;
    }

    /// <summary>
    /// Écrit un <c>demarrage</c> puis 288 relevés à <paramref name="minuitUtc"/> + i × 5 min : <c>r5</c> avance de
    /// 5 h toutes les 60 tranches et <c>u5</c> retombe à <paramref name="u5Depart"/> après chaque reset ; <c>u7</c>
    /// croît ; <c>r7</c> = samedi suivant 22:00Z (= samedi 00:00 heure de Paris en été, comme constaté).
    /// Rend le chemin du dossier.
    /// </summary>
    public static string JourneeNominale(DateTimeOffset minuitUtc, double u5Depart = 0.0, double pas = 0.003)
    {
        var dossier = DossierTemp();
        var horloge = new FakeClock(minuitUtc);
        var journal = new JournalReleves(dossier, horloge);

        journal.AjouterEvenement(new EvenementJournal(minuitUtc, TypeEvenement.Demarrage, Version: "tests"));

        var r7 = SamediSuivant22Z(minuitUtc);
        for (var i = 0; i < RelevesParJour; i++)
        {
            var t = minuitUtc + i * Cadence;
            horloge.UtcNow = t;
            var r5 = minuitUtc + (i / TranchesParFenetre + 1) * Fenetre5h;
            var u5 = u5Depart + (i % TranchesParFenetre) * pas;
            var u7 = 0.30 + i * 0.001;
            journal.AjouterReleve(new ReleveJournal(t, SourceUsage.SondeEnTetes,
                U5: u5, R5: r5, Statut5: StatutServeur.Autorise,
                U7: u7, R7: r7, Statut7: StatutServeur.Autorise,
                Overage: null, OverageStatut: null));
        }

        return dossier;
    }

    // Le premier samedi STRICTEMENT après la date UTC de minuit, à 22:00Z de la veille (vendredi 22:00Z
    // = samedi 00:00 en heure d'été de Paris) — un r7 plausible pour une journée de septembre.
    private static DateTimeOffset SamediSuivant22Z(DateTimeOffset minuitUtc)
    {
        var jour = minuitUtc.UtcDateTime.Date.AddDays(1);
        while (jour.DayOfWeek != DayOfWeek.Saturday) jour = jour.AddDays(1);
        return new DateTimeOffset(jour, TimeSpan.Zero) - TimeSpan.FromHours(2);
    }
}
