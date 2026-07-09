using Chronos.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le formateur FR abrégé des tokens (NET-02) : abréviation M/k à 1 décimale, virgule
/// française, suppression de la décimale « ,0 », cas brut &lt; 1000, bornes (0 et négatif → 0).
/// Égalité de chaîne EXACTE (un seul espace, pas d'unité vide).
/// </summary>
public class TokenFormatterTests
{
    [Fact]
    public void Format_millions_abrege_avec_virgule_fr()
        => Assert.Equal("≈ 62,5 M tokens", TokenFormatter.Format(62_484_658));

    [Fact]
    public void Format_million_rond_supprime_la_decimale_zero()
        => Assert.Equal("≈ 1 M tokens", TokenFormatter.Format(1_000_000));

    [Fact]
    public void Format_milliers_abrege_avec_virgule_fr()
        => Assert.Equal("≈ 12,3 k tokens", TokenFormatter.Format(12_345));

    [Fact]
    public void Format_petit_nombre_reste_brut()
        => Assert.Equal("≈ 500 tokens", TokenFormatter.Format(500));

    [Fact]
    public void Format_zero()
        => Assert.Equal("≈ 0 tokens", TokenFormatter.Format(0));

    [Fact]
    public void Format_negatif_traite_comme_zero()
        => Assert.Equal("≈ 0 tokens", TokenFormatter.Format(-42));
}
