using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// BRA-01 (plan 41-01) — position angulaire PURE d'une braise (degrés, 0 = midi, sens horaire). L'anneau 5 h de Braises
/// compte 20 braises de 15 min en 5 groupes d'une heure : groupe centré dans son secteur de 72°, pas de 13,2° dans un
/// groupe, vide de 32,4° entre groupes, midi au milieu d'un vide. Plan 43-05 (écart du constat) : l'anneau hebdo compte
/// 14 braises en 7 groupes de 2 — un groupe par jour, une braise par demi-journée, secteur de 360/7 ≈ 51,43°, pas de 15,6°.
/// </summary>
public class BraisesGeometrieTests
{
    [Theory]
    [InlineData(0, 16.2)]
    [InlineData(1, 29.4)]
    [InlineData(2, 42.6)]
    [InlineData(3, 55.8)]
    [InlineData(4, 88.2)]
    [InlineData(19, 343.8)]
    public void Vingt_braises_en_cinq_groupes_de_quatre(int i, double attendu)
        => Assert.Equal(attendu, BraisesGeometrie.Angle(i, 20, 4, 13.2), 6);

    [Fact]
    public void Pas_dans_un_groupe_vide_entre_groupes_et_midi_au_milieu_du_vide()
    {
        static double Ecart(int a, int b) => BraisesGeometrie.Angle(b, 20, 4, 13.2) - BraisesGeometrie.Angle(a, 20, 4, 13.2);

        for (int g = 0; g < 5; g++)
            for (int k = 0; k < 3; k++)
                Assert.Equal(13.2, Ecart(g * 4 + k, g * 4 + k + 1), 6);

        foreach (var fin in new[] { 3, 7, 11, 15 })
            Assert.Equal(32.4, Ecart(fin, fin + 1), 6);

        // Symétrie autour de midi : le vide 343,8° → 376,2° est centré sur 360°.
        Assert.Equal(BraisesGeometrie.Angle(0, 20, 4, 13.2), 360 - BraisesGeometrie.Angle(19, 20, 4, 13.2), 6);
    }

    [Fact]
    public void Sans_groupe_repartition_uniforme()
    {
        for (int i = 0; i < 12; i++)
            Assert.Equal(i * 30.0, BraisesGeometrie.Angle(i, 12, 1, 0), 6);
    }

    [Fact]
    public void Pas_trop_large_compte_non_multiple_ou_pas_nul_retombent_en_uniforme()
    {
        Assert.Equal(18.0, BraisesGeometrie.Angle(1, 20, 4, 30), 6);     // 3 × 30 ≥ 72
        Assert.Equal(20.0, BraisesGeometrie.Angle(1, 18, 4, 13.2), 6);   // 18 non multiple de 4
        Assert.Equal(18.0, BraisesGeometrie.Angle(1, 20, 4, 0), 6);      // pas nul
        Assert.Equal(0.0, BraisesGeometrie.Angle(0, 0, 1, 0), 6);        // count 0 → n = 1, pas de division par zéro
    }

    // --- Plan 43-05 : anneau hebdo en 7 groupes de 2 (un jour par groupe) ---

    private const double PasHebdo = 15.6;
    private static double Hebdo(int i) => BraisesGeometrie.Angle(i, 14, 2, PasHebdo);

    [Fact]
    public void Quatorze_braises_en_sept_groupes_de_deux_centres_dans_leur_secteur()
    {
        double secteur = 360.0 / 7;
        for (int g = 0; g < 7; g++)
        {
            double centre = g * secteur + secteur / 2;
            Assert.Equal(centre - PasHebdo / 2, Hebdo(2 * g), 6);
            Assert.Equal(centre + PasHebdo / 2, Hebdo(2 * g + 1), 6);
        }
        Assert.NotEqual(13 * 360.0 / 14, Hebdo(13), 3);   // pas le repli uniforme
    }

    [Fact]
    public void Hebdo_le_vide_entre_jours_vaut_plus_du_double_de_l_ecart_intra_groupe_et_midi_est_dans_un_vide()
    {
        for (int g = 0; g < 7; g++)
            Assert.Equal(PasHebdo, Hebdo(2 * g + 1) - Hebdo(2 * g), 6);

        for (int g = 0; g < 6; g++)
        {
            double vide = Hebdo(2 * g + 2) - Hebdo(2 * g + 1);
            Assert.True(vide >= 2 * PasHebdo, $"vide {vide:F2}° < 2 × {PasHebdo}°");
        }

        // Symétrie autour de midi : le vide qui enjambe 0° est centré sur midi.
        Assert.Equal(Hebdo(0), 360 - Hebdo(13), 6);
        Assert.True(Hebdo(0) > PasHebdo, "midi trop proche de la première braise");
    }

    [Theory]
    [InlineData(128.0, 11)]   // 5 j 8 h restants : 5 jours pleins + 1 demi-journée
    [InlineData(24.0, 2)]     // 1 jour : un groupe
    [InlineData(0.0, 0)]
    [InlineData(168.0, 14)]
    public void Hebdo_braises_allumees_pour_un_temps_restant(double heuresRestantes, int attendu)
    {
        // Même calcul que EmberRingControl : Math.Round(frac × count, AwayFromZero).
        double frac = heuresRestantes / 168.0;
        Assert.Equal(attendu, (int)Math.Round(frac * 14, MidpointRounding.AwayFromZero));
    }
}
