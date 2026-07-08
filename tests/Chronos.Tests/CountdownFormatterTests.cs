using System;
using Chronos.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le formatage FR pur d'un compte à rebours (RAF-03, présentation).
/// CountdownFormatter.Format est une fonction pure sans dépendance WPF ni CultureInfo :
/// littéraux français fixes, minutes sur 2 chiffres au-delà de l'heure, garde du temps écoulé.
/// </summary>
public class CountdownFormatterTests
{
    // ≥ 1 jour : "{j} j {h} h" (les minutes tombent, la précision au jour/heure suffit).
    [Fact]
    public void Format_au_dela_du_jour_affiche_jours_et_heures()
    {
        var reste = new TimeSpan(days: 3, hours: 14, minutes: 27, seconds: 0);
        Assert.Equal("3 j 14 h", CountdownFormatter.Format(reste));
    }

    // ≥ 1 h : "{h} h {mm}" — minutes sur 2 chiffres (ex. "2 h 05").
    [Fact]
    public void Format_au_dela_de_l_heure_affiche_heures_et_minutes_sur_deux_chiffres()
    {
        var reste = new TimeSpan(hours: 2, minutes: 5, seconds: 0);
        Assert.Equal("2 h 05", CountdownFormatter.Format(reste));
    }

    // < 1 h : "{m} min".
    [Fact]
    public void Format_en_dessous_de_l_heure_affiche_les_minutes()
    {
        var reste = TimeSpan.FromMinutes(45);
        Assert.Equal("45 min", CountdownFormatter.Format(reste));
    }

    // Temps écoulé (zéro ou négatif) : "0 min" (jamais de valeur négative affichée).
    [Theory]
    [InlineData(0)]
    [InlineData(-120)]
    public void Format_temps_ecoule_ou_negatif_affiche_zero_min(int secondes)
    {
        var reste = TimeSpan.FromSeconds(secondes);
        Assert.Equal("0 min", CountdownFormatter.Format(reste));
    }
}
