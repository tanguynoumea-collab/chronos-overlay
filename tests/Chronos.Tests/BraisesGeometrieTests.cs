using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// BRA-01 (plan 41-01) — position angulaire PURE d'une braise (degrés, 0 = midi, sens horaire). L'anneau 5 h de Braises
/// compte 20 braises de 15 min en 5 groupes d'une heure : groupe centré dans son secteur de 72°, pas de 13,2° dans un
/// groupe, vide de 32,4° entre groupes, midi au milieu d'un vide. L'anneau hebdo (12, sans groupe) reste uniforme.
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
    public void Anneau_hebdo_inchange_repartition_uniforme()
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
}
