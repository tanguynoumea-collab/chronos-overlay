using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// BRA-01 (plan 41-01) — position angulaire PURE d'une braise (degrés, 0 = midi, sens horaire). L'anneau 5 h de Braises
/// compte (plan 43-08) 25 braises de 12 min en 5 groupes d'une heure : groupe centré dans son secteur de 72°, pas de 11° dans
/// un groupe, vide de 28° entre groupes, midi au milieu d'un vide. Plan 43-05 (écart du constat) : l'anneau hebdo compte
/// 14 braises en 7 groupes de 2 — un groupe par jour, une braise par demi-journée, secteur de 360/7 ≈ 51,43°, pas de 15,6°.
/// </summary>
public class BraisesGeometrieTests
{
    // Plan 43-08 (constat du 2026-10-05) : 25 braises de 12 min en 5 groupes de 5, pas de 11° → largeur 44°, vide 28°.
    private const double Pas5h = 11.0;
    private static double CinqHeures(int i) => BraisesGeometrie.Angle(i, 25, 5, Pas5h);

    [Theory]
    [InlineData(0, 14.0)]
    [InlineData(1, 25.0)]
    [InlineData(2, 36.0)]
    [InlineData(3, 47.0)]
    [InlineData(4, 58.0)]
    [InlineData(5, 86.0)]
    [InlineData(24, 346.0)]
    public void Vingt_cinq_braises_en_cinq_groupes_de_cinq(int i, double attendu)
        => Assert.Equal(attendu, CinqHeures(i), 6);

    [Fact]
    public void Pas_dans_un_groupe_vide_entre_groupes_et_midi_au_milieu_du_vide()
    {
        for (int g = 0; g < 5; g++)
        {
            // Groupe centré dans son secteur de 72°.
            Assert.Equal(g * 72 + 36, (CinqHeures(5 * g) + CinqHeures(5 * g + 4)) / 2, 6);
            for (int k = 0; k < 4; k++)
                Assert.Equal(Pas5h, CinqHeures(5 * g + k + 1) - CinqHeures(5 * g + k), 6);
        }

        foreach (var fin in new[] { 4, 9, 14, 19 })
        {
            double vide = CinqHeures(fin + 1) - CinqHeures(fin);
            Assert.True(vide >= 2 * Pas5h, $"vide {vide:F2}° < 2 × {Pas5h}°");
        }

        // Symétrie autour de midi : le vide 346° → 374° est centré sur 360°.
        Assert.Equal(CinqHeures(0), 360 - CinqHeures(24), 6);
    }

    [Fact]
    public void Les_braises_5h_sont_distinctes_au_rayon_66()
    {
        // Écart centre à centre (corde) > 2 × PipRadius (4) + 2 px : les braises ne se touchent pas.
        double corde = 2 * 66 * Math.Sin(Pas5h / 2 * Math.PI / 180);
        Assert.True(corde > 2 * 4.0 + 2, $"corde {corde:F2} px");
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
