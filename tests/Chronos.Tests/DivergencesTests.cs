using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// D-34-07 — la divergence « consommé ailleurs » est une ANALYSE de <c>Services/Historique</c>, pas une géométrie : une marche
/// du % hebdo (Σ des Δ positifs d'une case d'une heure ≥ <see cref="Divergences.SeuilDelta"/>) sur une heure SANS tranche de
/// tokens Claude Code ET dont les transcripts sont COUVERTS. Une heure « hors couverture » ou « transcripts absents » ne peut
/// jamais accuser Cowork : on ne sait pas ce que Claude Code y a fait. Les heures adjacentes retenues fusionnent en une seule
/// divergence qui porte la somme des Δ. Tests purs, sans STA : barres construites à la main, aucun fichier.
/// </summary>
public class DivergencesTests
{
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset R7 = Utc("2026-09-25T22:00:00Z");

    private static DeltaConsommation Delta(string a, double delta)
        => new(WindowKind.SevenDay, Utc(a).AddMinutes(-5), Utc(a), delta, R7, Anormal: delta < 0);

    private static BarreHeure Barre(string debut, int n, EtatCouverture etat = EtatCouverture.Couverte)
        => new(Utc(debut), "", n == 0 ? TotauxTokens.Vide : new TotauxTokens(1000, 800, 0, 0, n), TotauxTokens.Vide, etat);

    // Plage [18:00Z, 23:00Z[ : cinq barres ; 19:00Z et 20:00Z sans aucun token, les autres avec 6 messages.
    private static IReadOnlyList<BarreHeure> CinqBarres(EtatCouverture etat19 = EtatCouverture.Couverte, EtatCouverture etat20 = EtatCouverture.Couverte)
        => new[]
        {
            Barre("2026-09-23T18:00:00Z", 6),
            Barre("2026-09-23T19:00:00Z", 0, etat19),
            Barre("2026-09-23T20:00:00Z", 0, etat20),
            Barre("2026-09-23T21:00:00Z", 6),
            Barre("2026-09-23T22:00:00Z", 6),
        };

    private static IReadOnlyList<DeltaConsommation> TroisMarches()
        => new[] { Delta("2026-09-23T19:20:00Z", 0.02), Delta("2026-09-23T20:40:00Z", 0.02), Delta("2026-09-23T21:10:00Z", 0.01) };

    [Fact]
    public void Une_marche_hebdo_sans_tranche_couverte_est_une_divergence()
    {
        var divergences = Divergences.Detecter(TroisMarches(), CinqBarres());

        var d = Assert.Single(divergences);
        Assert.Equal(Utc("2026-09-23T19:00:00Z"), d.Debut);   // 19:00Z et 20:00Z adjacentes → fusionnées
        Assert.Equal(Utc("2026-09-23T21:00:00Z"), d.Fin);     // 21:00Z porte des tokens → exclue
        Assert.Equal(0.04, d.Delta, 9);
    }

    [Fact]
    public void Une_heure_hors_couverture_ou_transcripts_absents_ne_peut_pas_accuser()
    {
        var divergences = Divergences.Detecter(TroisMarches(), CinqBarres(EtatCouverture.TranscriptsAbsents, EtatCouverture.HorsCouverture));

        Assert.Empty(divergences);
    }

    [Fact]
    public void Le_seuil_est_la_granularite_des_en_tetes()
    {
        Assert.Equal(0.01, Divergences.SeuilDelta);

        var sousLeSeuil = Divergences.Detecter(new[] { Delta("2026-09-23T19:20:00Z", 0.009) }, CinqBarres());
        Assert.Empty(sousLeSeuil);

        var deuxDemis = Divergences.Detecter(new[] { Delta("2026-09-23T19:10:00Z", 0.005), Delta("2026-09-23T19:40:00Z", 0.005) }, CinqBarres());
        var d = Assert.Single(deuxDemis);   // la somme par heure atteint le seuil
        Assert.Equal(Utc("2026-09-23T19:00:00Z"), d.Debut);
        Assert.Equal(Utc("2026-09-23T20:00:00Z"), d.Fin);
    }

    [Fact]
    public void Les_deltas_negatifs_ne_comptent_pas()
    {
        // Un Δ anormal (négatif sans reset) n'est jamais SOUSTRAIT : seule la somme des positifs compte.
        var divergences = Divergences.Detecter(new[] { Delta("2026-09-23T19:10:00Z", -0.03), Delta("2026-09-23T19:40:00Z", 0.01) }, CinqBarres());

        var d = Assert.Single(divergences);
        Assert.Equal(0.01, d.Delta, 9);
    }
}
