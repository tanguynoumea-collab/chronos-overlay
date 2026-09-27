using Chronos.Models.Historique;
using Chronos.Rendering.Historique;
using Chronos.Services.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 — les ÉCHELLES pures de la fenêtre Historique : temps → x (<see cref="EchelleTemps"/>) et valeur → y
/// (<see cref="EchelleValeur"/>). Aucun WPF, aucune horloge : une <c>Plage</c> vient de <c>BornesPlage</c> (fuseau
/// injecté), une largeur et une hauteur sont des doubles nus.
///
/// Ce que ces tests gravent : <c>X</c> est linéaire sur la DURÉE RÉELLE de la plage (169 h la semaine du 25/10/2026 :
/// le mardi minuit n'est PAS à 3/7 de la largeur) ; <c>Instant</c> est l'inverse de <c>X</c>, borné à la plage ; <c>X</c>
/// et <c>Fraction</c> ne sont PAS bornés (la semaine précédente et « maintenant » peuvent déborder) ; une plage vide ne
/// divise pas par zéro ; <c>Y</c> inverse l'axe et borne la valeur ; <c>MaxArrondi</c> choisit un plafond « joli » (D-34-12) ;
/// <c>MaxRythme</c> vaut 0,25 (échelle « 0 – 25 % » du plan de design §2.2).
/// </summary>
public class EchellesHistoriqueTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    // La semaine de forfait du 19 au 26 septembre 2026 : 168 h pile (samedi 00:00 Paris = vendredi 22:00Z en été).
    private static readonly Plage Semaine168 = new(Utc("2026-09-18T22:00:00Z"), Utc("2026-09-25T22:00:00Z"));

    [Fact]
    public void X_est_lineaire_sur_la_plage_et_Instant_l_inverse()
    {
        var debut = Semaine168.Debut;

        Assert.Equal(0.0, EchelleTemps.X(debut, Semaine168, 840), 9);
        Assert.Equal(840.0, EchelleTemps.X(Semaine168.Fin, Semaine168, 840), 9);
        Assert.Equal(420.0, EchelleTemps.X(debut + TimeSpan.FromHours(84), Semaine168, 840), 9);

        Assert.Equal(debut + TimeSpan.FromHours(84), EchelleTemps.Instant(420, Semaine168, 840));

        var t = debut + TimeSpan.FromHours(37) + TimeSpan.FromMinutes(13);
        var allerRetour = EchelleTemps.Instant(EchelleTemps.X(t, Semaine168, 840), Semaine168, 840);
        Assert.True(Math.Abs((allerRetour - t).TotalSeconds) < 1.0, $"Instant(X(t)) = {allerRetour:O} ≠ t = {t:O}");
    }

    [Fact]
    public void Instant_est_borne_a_la_plage_et_X_ne_l_est_pas()
    {
        Assert.Equal(Semaine168.Debut, EchelleTemps.Instant(-10, Semaine168, 840));
        Assert.Equal(Semaine168.Fin, EchelleTemps.Instant(9999, Semaine168, 840));

        Assert.True(EchelleTemps.X(Semaine168.Debut - TimeSpan.FromHours(1), Semaine168, 840) < 0);
        Assert.True(EchelleTemps.Fraction(Semaine168.Fin + TimeSpan.FromHours(1), Semaine168) > 1);

        // Plage vide : aucune exception, aucune division par zéro.
        var vide = new Plage(Semaine168.Debut, Semaine168.Debut);
        Assert.Equal(0.0, EchelleTemps.Fraction(Semaine168.Debut + TimeSpan.FromHours(3), vide));
        Assert.Equal(Semaine168.Debut, EchelleTemps.Instant(100, Semaine168, 0));   // largeur nulle → Debut
    }

    [Fact]
    public void Une_semaine_de_169_heures_place_le_mardi_minuit_a_sa_vraie_fraction()
    {
        // La semaine du 24 au 31 octobre 2026 contient le passage à l'heure d'hiver : 169 h (BornesPlageTests).
        var plage = BornesPlage.SemaineDeForfait(Utc("2026-10-27T12:00:00Z"), resetHebdoObserve: null, ancre: null, Tz);
        Assert.Equal(TimeSpan.FromHours(169), plage.Duree);

        var mardiMinuit = BornesPlage.Jour(Utc("2026-10-27T12:00:00Z"), Tz).Debut;   // 2026-10-26T23:00Z (heure d'hiver)

        Assert.Equal(73.0 / 169.0, EchelleTemps.Fraction(mardiMinuit, plage), 9);
        Assert.NotEqual(3.0 / 7.0, EchelleTemps.Fraction(mardiMinuit, plage), 6);   // et PAS 3/7 : la semaine n'a pas 168 h
    }

    [Fact]
    public void Largeur_d_un_intervalle_est_positive_ou_nulle()
    {
        var t0 = Semaine168.Debut + TimeSpan.FromHours(10);
        var t1 = Semaine168.Debut + TimeSpan.FromHours(20);

        Assert.Equal(0.0, EchelleTemps.Largeur(t1, t0, Semaine168, 840));   // intervalle inversé → 0, jamais négatif
        Assert.Equal(120.0, EchelleTemps.Largeur(Semaine168.Debut, Semaine168.Debut + TimeSpan.FromHours(24), Semaine168, 840), 9);
    }

    [Fact]
    public void Y_inverse_l_axe_et_borne_la_valeur()
    {
        Assert.Equal(150.0, EchelleValeur.Y(0, 1, 150), 9);
        Assert.Equal(0.0, EchelleValeur.Y(1, 1, 150), 9);
        Assert.Equal(75.0, EchelleValeur.Y(0.5, 1, 150), 9);
        Assert.Equal(0.0, EchelleValeur.Y(1.3, 1, 150), 9);       // au-dessus du max : plafonné en haut
        Assert.Equal(150.0, EchelleValeur.Y(-0.2, 1, 150), 9);    // négatif : au plancher
        Assert.Equal(150.0, EchelleValeur.Y(double.NaN, 1, 150), 9);
        Assert.Equal(150.0, EchelleValeur.Y(0.1, 0, 150), 9);     // max nul : rien à dessiner, pas de division par zéro
        Assert.Equal(36.0, EchelleValeur.Y(0.125, EchelleValeur.MaxRythme, 72), 9);
    }

    [Fact]
    public void MaxArrondi_choisit_le_plafond_joli_superieur()
    {
        Assert.Equal(1_200_000, EchelleValeur.MaxArrondi(1_150_000));   // « 0 – 1,2 M »
        Assert.Equal(1_200_000, EchelleValeur.MaxArrondi(1_200_000));   // déjà joli : inchangé
        Assert.Equal(250_000, EchelleValeur.MaxArrondi(250_000));
        Assert.Equal(1_000, EchelleValeur.MaxArrondi(999));
        Assert.Equal(8, EchelleValeur.MaxArrondi(7));
        Assert.Equal(1, EchelleValeur.MaxArrondi(0));
        Assert.Equal(1, EchelleValeur.MaxArrondi(-5));
        Assert.Equal(10_000_000, EchelleValeur.MaxArrondi(8_100_000));
    }

    [Fact]
    public void MaxRythme_vaut_un_quart()
    {
        // L'échelle « 0 – 25 % » de la piste Rythme (DESIGN_PLAN §2.2) : épinglée pour qu'une « optimisation » ne la
        // change pas en silence.
        Assert.Equal(0.25, EchelleValeur.MaxRythme);
    }
}
