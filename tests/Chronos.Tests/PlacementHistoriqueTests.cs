using Chronos.Placement;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-01 — bornage PUR de la géométrie persistée de la fenêtre Historique (Pitfall 2 de la recherche 34 : un moniteur
/// débranché ne doit pas cacher la fenêtre). Aucun WPF : l'écran virtuel est passé en paramètre, la fenêtre applique le résultat.
/// </summary>
public class PlacementHistoriqueTests
{
    private static readonly RectangleEcran Ecran = new(0, 0, 1920, 1080);

    [Fact]
    public void Une_geometrie_absente_rend_null()
    {
        Assert.Null(PlacementHistorique.Borner((null, null, null, null), Ecran, 760, 480));
        Assert.Null(PlacementHistorique.Borner((100, 50, 900, null), Ecran, 760, 480));
        Assert.Null(PlacementHistorique.Borner((null, 50, 900, 600), Ecran, 760, 480));
    }

    [Fact]
    public void Une_geometrie_hors_ecran_est_ramenee_dans_l_ecran_virtuel()
    {
        var droite = PlacementHistorique.Borner((99999, 50, 900, 600), Ecran, 760, 480);
        Assert.NotNull(droite);
        Assert.Equal(1920 - 900, droite!.Value.X);
        Assert.Equal(50, droite.Value.Y);

        var gauche = PlacementHistorique.Borner((-5000, -5000, 900, 600), Ecran, 760, 480);
        Assert.Equal((0d, 0d), (gauche!.Value.X, gauche.Value.Y));

        // Écran virtuel qui commence à gauche du primaire : une abscisse négative y est légitime.
        var virtuel = new RectangleEcran(-1920, 0, 3840, 1080);
        var aGauche = PlacementHistorique.Borner((-1000, 100, 900, 600), virtuel, 760, 480);
        Assert.Equal((-1000d, 100d, 900d, 600d), aGauche!.Value);
    }

    [Fact]
    public void La_taille_est_bornee_aux_minima_et_a_l_ecran()
    {
        var petite = PlacementHistorique.Borner((10, 10, 300, 200), Ecran, 760, 480);
        Assert.Equal(760, petite!.Value.Largeur);
        Assert.Equal(480, petite.Value.Hauteur);

        var enorme = PlacementHistorique.Borner((10, 10, 5000, 4000), Ecran, 760, 480);
        Assert.Equal(1920, enorme!.Value.Largeur);
        Assert.Equal(1080, enorme.Value.Hauteur);
        Assert.Equal(0, enorme.Value.X);   // re-borné après la réduction de taille
        Assert.Equal(0, enorme.Value.Y);
    }
}
