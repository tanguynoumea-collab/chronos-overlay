using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Chronos.Converters;
using Chronos.Models.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-02 / HIS-07 (plan 34-04) — les trois convertisseurs XAML qui posent les annotations <c>TextBlock</c> des vues
/// Semaine et Jour sur l'axe du temps des pistes, sans une ligne de C# dans les vues : instant → x borné à la piste,
/// intervalle → largeur positive (finissant à « maintenant » si la fin manque), fraction → largeur (barre F2). Tous
/// tolèrent <c>UnsetValue</c>, <c>null</c> et les types inattendus en rendant 0 : un binding pas encore résolu ne fait
/// jamais lever la vue. Faits purs, sans STA.
/// </summary>
public class HistoriqueConvertersTests
{
    private static readonly Plage Plage168 = new(
        new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.FromHours(2)),
        new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.FromHours(2)));

    private static DateTimeOffset Debut => Plage168.Debut;

    private static object? Convertir(IMultiValueConverter c, params object[] values)
        => c.Convert(values, typeof(double), null, CultureInfo.InvariantCulture);

    [Fact]
    public void InstantVersX_borne_a_la_largeur()
    {
        var c = new InstantVersXConverter();
        Assert.Equal(420.0, Convertir(c, Debut.AddHours(84), Plage168, 840.0));
        Assert.Equal(0.0, Convertir(c, Debut.AddHours(-1), Plage168, 840.0));
        Assert.Equal(840.0, Convertir(c, Plage168.Fin.AddHours(1), Plage168, 840.0));
        Assert.Equal(0.0, Convertir(c, DependencyProperty.UnsetValue, Plage168, 840.0));
        Assert.Equal(0.0, Convertir(c, Debut.AddHours(84), null!, 840.0));
        Assert.Equal(0.0, Convertir(c, Debut.AddHours(84), Plage168, double.NaN));
        Assert.Throws<NotSupportedException>(() => { c.ConvertBack(420.0, new[] { typeof(DateTimeOffset) }, null, CultureInfo.InvariantCulture); });
    }

    [Fact]
    public void LargeurIntervalle_est_positive_et_finit_a_now_si_fin_nulle()
    {
        var c = new LargeurIntervalleConverter();
        Assert.Equal(10.0, Convertir(c, Debut.AddHours(1), Debut.AddHours(3), Plage168, 840.0));
        Assert.Equal(0.0, Convertir(c, Debut.AddHours(3), Debut.AddHours(1), Plage168, 840.0));
        Assert.Equal(20.0, Convertir(c, Debut.AddHours(1), null!, Plage168, 840.0, Debut.AddHours(5)));
        Assert.Equal(0.0, Convertir(c, Debut.AddHours(1), null!, Plage168, 840.0));
        Assert.Equal(0.0, Convertir(c, Debut.AddHours(1), DependencyProperty.UnsetValue, Plage168, 840.0));
        // Un intervalle qui déborde la plage est coupé à la piste : la largeur suit l'x borné d'InstantVersX.
        Assert.Equal(5.0, Convertir(c, Debut.AddHours(-1), Debut.AddHours(1), Plage168, 840.0));
    }

    /// <summary>35-04 (D-35-17) — l'infobulle du réticule reste dans sa piste : <c>[x, largeurInfobulle, largeurCanvas]</c> →
    /// <c>clamp(x, 0, max(0, W − w))</c>.</summary>
    [Fact]
    public void BorneInfobulle_garde_l_infobulle_dans_la_piste()
    {
        var c = new BorneInfobulleConverter();
        Assert.Equal(280.0, Convertir(c, 300.0, 120.0, 400.0));   // au bord droit : recule de ce qui déborde
        Assert.Equal(10.0, Convertir(c, 10.0, 120.0, 400.0));    // au milieu : inchangée
        Assert.Equal(0.0, Convertir(c, -5.0, 120.0, 400.0));     // jamais à gauche de la piste
        Assert.Equal(0.0, Convertir(c, 300.0, 500.0, 400.0));    // plus large que la piste : collée à gauche
        Assert.Equal(280.0, Convertir(c, 280.0, 120.0, 400.0));   // pile au bord : inchangée
    }

    [Fact]
    public void BorneInfobulle_est_tolerante_et_ne_revient_pas()
    {
        var c = new BorneInfobulleConverter();
        Assert.Equal(0.0, Convertir(c, DependencyProperty.UnsetValue, 120.0, 400.0));
        Assert.Equal(0.0, Convertir(c, null!, 120.0, 400.0));
        Assert.Equal(0.0, Convertir(c, double.NaN, 120.0, 400.0));
        Assert.Equal(0.0, Convertir(c, 300.0));
        // Largeurs pas encore mesurées (infobulle repliée, canvas non arrangé) : l'abscisse brute, bornée à gauche.
        Assert.Equal(300.0, Convertir(c, 300.0, DependencyProperty.UnsetValue, DependencyProperty.UnsetValue));
        Assert.Equal(300.0, Convertir(c, 300.0, 120.0, double.NaN));
        Assert.Throws<NotSupportedException>(() => { c.ConvertBack(0.0, new[] { typeof(double) }, null, CultureInfo.InvariantCulture); });
    }

    [Fact]
    public void FractionVersLargeur_multiplie_et_borne()
    {
        var c = new FractionVersLargeurConverter();
        Assert.Equal(150.0, Convertir(c, 0.5, 300.0));
        Assert.Equal(300.0, Convertir(c, 1.3, 300.0));
        Assert.Equal(0.0, Convertir(c, -1.0, 300.0));
        Assert.Equal(0.0, Convertir(c, double.NaN, 300.0));
        Assert.Equal(0.0, Convertir(c, DependencyProperty.UnsetValue, 300.0));
    }
}
