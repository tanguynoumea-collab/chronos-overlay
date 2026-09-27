using System.Globalization;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Chronos.Text;
using Chronos.ViewModels.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// D-34-16 — la semaine de RÉFÉRENCE des maquettes (DESIGN_PLAN §8), fabriquée EN MÉMOIRE par <see cref="ScenariosHistorique"/>
/// et servie par <see cref="SourceHistoriqueDemonstration"/> : la galerie <c>--historique</c> (34-05) et les tests d'honnêteté
/// (34-08) consomment les MÊMES objets — ce que l'utilisateur voit est ce que les tests prouvent. Ces tests gravent les FAITS du
/// scénario : deux trous avec leurs causes, le saut hebdo de la nuit, la fenêtre 5 h épuisée du mercredi soir, la divergence
/// mer. 21 h → 23 h et nulle part ailleurs, la semaine précédente ouverte le 14 sept., le jour de référence et sa légende.
/// Déterministe (aucun <c>Random</c>, aucune horloge système), fuseau de Paris injecté.
/// </summary>
public class ScenariosHistoriqueTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static readonly TimeSpan Cadence = RateLimitHeaderUsageProvider.CadenceNominale;
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset Now = ScenariosHistorique.Maintenant(Tz);
    private static readonly Plage Semaine = ScenariosHistorique.Semaine(Tz);
    private static readonly Plage SemainePrecedente = BornesPlage.SemaineDeForfait(Semaine.Debut.AddTicks(-1), Semaine.Debut, null, Tz);

    private static AnalyseJournal AnalyseCourante() => AnalyseReleves.Analyser(ScenariosHistorique.Journal(Semaine, Tz), Now, Cadence);

    [Fact]
    public void La_semaine_de_reference_va_du_samedi_19_au_samedi_26_septembre()
    {
        Assert.Equal(new Plage(Utc("2026-09-18T22:00:00Z"), Utc("2026-09-25T22:00:00Z")), Semaine);
        Assert.Equal(Utc("2026-09-24T15:12:00Z"), Now);
        Assert.Equal(Utc("2026-09-25T22:00:00Z"), ScenariosHistorique.RepereHebdo);
        Assert.Equal(Utc("2026-09-18T22:00:00Z"), ScenariosHistorique.RepereHebdoPrecedent);
        Assert.Equal(Utc("2026-09-14T10:00:00Z"), ScenariosHistorique.JournalOuvertLe);
        Assert.Equal(new Plage(Utc("2026-09-11T22:00:00Z"), Utc("2026-09-18T22:00:00Z")), SemainePrecedente);
    }

    [Fact]
    public void Le_journal_a_ses_deux_trous_avec_leurs_causes()
    {
        var a = AnalyseCourante();

        Assert.Equal(SourceUsage.SondeEnTetes, a.Source);
        Assert.Equal(2, a.Trous.Count);
        Assert.Equal(Utc("2026-09-22T21:00:00Z"), a.Trous[0].Debut);   // mar. 23:00 local
        Assert.Equal(Utc("2026-09-23T05:00:00Z"), a.Trous[0].Fin);     // mer. 07:00 local
        Assert.Equal(CauseTrou.ChronosArrete, a.Trous[0].Cause);
        Assert.Equal(Utc("2026-09-24T12:00:00Z"), a.Trous[1].Debut);   // jeu. 14:00 local
        Assert.Equal(Utc("2026-09-24T14:00:00Z"), a.Trous[1].Fin);     // jeu. 16:00 local
        Assert.Equal(CauseTrou.JetonInvalide, a.Trous[1].Cause);
        Assert.All(a.Trous, t => Assert.NotNull(t.Fin));              // aucun trou ouvert à 17:12
        Assert.Equal(Utc("2026-09-24T15:10:00Z"), a.Serie[^1].T);       // dernier relevé : il y a 2 min
    }

    /// <summary>
    /// Trou 0 (la nuit) : hebdo +0,02 « pendant l'absence », 5 h indéterminable (un reset dans les 8 h). Trou 1 (jeton invalide,
    /// 14 h → 16 h) : hebdo 0 ; 5 h indéterminable AUSSI — la grille de 5 h ancrée sur le début de la semaine (D-34-16) passe par
    /// jeu. 15:00 local (13:00Z), à l'intérieur de l'absence : c'est un fait de la grille, dit comme tel (« au moins un reset »).
    /// </summary>
    [Fact]
    public void Le_saut_de_la_nuit_est_hebdo_seulement()
    {
        var a = AnalyseCourante();

        var hebdoNuit = Assert.Single(a.Sauts, s => s.Fenetre == WindowKind.SevenDay && s.Trou == a.Trous[0]);
        Assert.NotNull(hebdoNuit.Delta);
        Assert.Equal(0.02, hebdoNuit.Delta!.Value, 9);
        var cinqNuit = Assert.Single(a.Sauts, s => s.Fenetre == WindowKind.FiveHour && s.Trou == a.Trous[0]);
        Assert.Null(cinqNuit.Delta);

        var hebdoJeton = Assert.Single(a.Sauts, s => s.Fenetre == WindowKind.SevenDay && s.Trou == a.Trous[1]);
        Assert.Equal(0.0, hebdoJeton.Delta!.Value, 9);
        var cinqJeton = Assert.Single(a.Sauts, s => s.Fenetre == WindowKind.FiveHour && s.Trou == a.Trous[1]);
        Assert.Null(cinqJeton.Delta);
        Assert.Contains(a.Resets5h, r => r.Instant == Utc("2026-09-24T13:00:00Z") && r.ObserveA == Utc("2026-09-24T14:00:00Z"));
    }

    [Fact]
    public void La_fenetre_du_mercredi_soir_est_epuisee_jusqu_au_reset()
    {
        var a = AnalyseCourante();
        var plateau = a.Serie.Where(r => Utc("2026-09-23T18:00:00Z") <= r.T && r.T < Utc("2026-09-23T22:00:00Z")).ToList();

        Assert.Equal(48, plateau.Count);
        Assert.All(plateau, r => { Assert.Equal(1.0, r.U5); Assert.Equal(StatutServeur.Rejete, r.Statut5); });
        Assert.True(a.Serie.Single(r => r.T == Utc("2026-09-23T17:55:00Z")).U5 < 1);
        Assert.True(a.Serie.First(r => r.T >= Utc("2026-09-23T22:00:00Z")).U5 < 0.1);
        Assert.Contains(a.Resets5h, r => r.Instant == Utc("2026-09-23T22:00:00Z"));
        Assert.Contains(a.Resets5h, r => r.Instant == Utc("2026-09-23T17:00:00Z"));
        Assert.Contains(a.Resets5h, r => r.Instant == Utc("2026-09-23T12:00:00Z"));
    }

    [Fact]
    public void La_divergence_est_le_mercredi_de_21_h_a_23_h_et_nulle_part_ailleurs()
    {
        var d = new SourceHistoriqueDemonstration(Tz).LireSemaine(Semaine, SemainePrecedente, Now);

        var divergence = Assert.Single(d.Divergences);
        Assert.Equal(Utc("2026-09-23T19:00:00Z"), divergence.Debut);
        Assert.Equal(Utc("2026-09-23T21:00:00Z"), divergence.Fin);
        Assert.Equal(0.04, divergence.Delta, 9);
        Assert.Equal(ScenariosHistorique.JournalOuvertLe, d.JournalOuvertLe);
        Assert.Equal(Now, d.LueA);
    }

    /// <summary>
    /// La couverture est celle que la VRAIE reconstruction produirait (<c>GarantirPasse(now, now)</c> → garanti jusqu'à <c>now</c>
    /// exclu) : toutes les heures passées sont couvertes, les heures à venir sont « transcripts absents » — pas « hors couverture ».
    /// </summary>
    [Fact]
    public void Les_tokens_couvrent_la_semaine_sauf_les_heures_voulues()
    {
        var d = new SourceHistoriqueDemonstration(Tz).LireSemaine(Semaine, SemainePrecedente, Now);
        var barres = d.Barres;

        Assert.Equal(168, barres.Count);
        Assert.All(barres.Where(b => b.DebutUtc < Now), b => Assert.Equal(EtatCouverture.Couverte, b.Etat));
        Assert.All(barres.Where(b => b.DebutUtc >= Now), b => Assert.Equal(EtatCouverture.TranscriptsAbsents, b.Etat));

        // Épuisée + Cowork : mer. 20:00 → jeu. 00:00 local sans un token Code.
        Assert.All(barres.Where(b => Utc("2026-09-23T18:00:00Z") <= b.DebutUtc && b.DebutUtc < Utc("2026-09-23T22:00:00Z")),
            b => Assert.Equal(0, b.Principal.N + b.SousAgents.N));
        // Les nuits (00:00 → 07:00 local) : rien.
        Assert.All(barres.Where(b => TimeZoneInfo.ConvertTime(b.DebutUtc, Tz).Hour < 7), b => Assert.Equal(0, b.Principal.N + b.SousAgents.N));
        // Le jeton invalide n'arrête pas Claude Code : jeu. 14:00 → 15:00 local a des tokens.
        var pendantLeJeton = barres.Single(b => b.DebutUtc == Utc("2026-09-24T12:00:00Z"));
        Assert.True(pendantLeJeton.Principal.N > 0);
        Assert.True(pendantLeJeton.Principal.Out > 0);
        // Chronos arrêté n'arrête pas Claude Code non plus : mar. 23:00 local a des tokens.
        Assert.True(barres.Single(b => b.DebutUtc == Utc("2026-09-22T21:00:00Z")).Principal.N > 0);

        var tranches = ScenariosHistorique.Agregats(Semaine, Tz).Tranches;
        var part = RenduLocalTokens.PartSousAgents(tranches);
        Assert.True(part.SousAgents.N > 0);
        Assert.True(part.Principal.N > part.SousAgents.N);
        Assert.All(tranches, t => Assert.Equal(TimeSpan.Zero, t.Slot.Offset));
    }

    /// <summary>
    /// Un trou n'est « ouvert » qu'à l'INSTANT de l'analyse ; pour une plage passée, cet instant est sa fin (sinon
    /// <c>AnalyseReleves</c> ajouterait un faux trou ouvert après le dernier relevé de toute semaine révolue).
    /// </summary>
    [Fact]
    public void La_semaine_precedente_commence_a_l_ouverture_du_journal()
    {
        var journal = ScenariosHistorique.Journal(SemainePrecedente, Tz);
        var p = AnalyseReleves.Analyser(journal, SemainePrecedente.Fin, Cadence);

        Assert.True(p.Serie.Count > 0);
        Assert.Equal(ScenariosHistorique.JournalOuvertLe, p.Serie[0].T);
        Assert.Empty(p.Trous);
        Assert.Equal(ScenariosHistorique.JournalOuvertLe, p.JournalOuvertLe);
        Assert.Equal(ScenariosHistorique.JournalOuvertLe, ScenariosHistorique.Journal(Semaine, Tz).JournalOuvertLe);
        Assert.InRange(p.Serie[^1].U7!.Value, 0.40, 0.60);
        Assert.All(p.Serie, r => Assert.Equal(ScenariosHistorique.RepereHebdoPrecedent, r.R7));
        Assert.Contains(journal.Evenements, e => e.Type == TypeEvenement.Demarrage && e.T == ScenariosHistorique.JournalOuvertLe);

        // Servie par la façade de démonstration, la semaine précédente n'a pas de trou non plus.
        var d = new SourceHistoriqueDemonstration(Tz).LireSemaine(Semaine, SemainePrecedente, Now);
        Assert.Empty(d.Precedente.Trous);
        Assert.Equal(p.Serie.Count, d.Precedente.Serie.Count);
    }

    [Fact]
    public void Le_jour_de_reference_a_ses_colonnes_et_sa_legende()
    {
        var source = new SourceHistoriqueDemonstration(Tz);
        var j = source.LireJour(BornesPlage.Jour(Now, Tz), Now);

        Assert.Equal(96, j.Colonnes.Count);
        var premiere = j.Colonnes.First(c => c.ParModele.Count > 0);
        Assert.Equal("opus · sonnet · haiku · sous-agents inclus", TextesHistorique.LegendeModeles(premiere.ParModele.Select(p => p.Model)));
        Assert.Single(j.Analyse.Trous);   // le jour de référence porte le trou « jeton invalide » (14:00 → 16:00)
        Assert.Equal(CauseTrou.JetonInvalide, j.Analyse.Trous[0].Cause);
        Assert.Equal(ScenariosHistorique.RepereHebdo, source.RepereHebdo(Now));

        var r = ScenariosHistorique.ReconstructionEnCours();
        Assert.Equal(PhaseReconstruction.Reconstruction, r.Phase);
        Assert.Equal(886, r.FichiersTraites);
        Assert.Equal(1603, r.FichiersTotal);
        Assert.True(r.SemaineCouranteDisponible);
        Assert.Equal(Now, ScenariosHistorique.HorlogeFigee(Tz).UtcNow);
    }
}
