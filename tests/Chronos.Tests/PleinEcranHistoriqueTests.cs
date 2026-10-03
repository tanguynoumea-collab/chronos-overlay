using System.IO;
using Chronos.Placement;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-10 / HIS-11 — décisions PURES du plein écran de l'Historique : Échap à deux niveaux et conversion des bornes physiques
/// d'un moniteur en DIP. Aucun WPF : la matrice TransformFromDevice et les bornes rcMonitor sont passées en paramètres.
/// </summary>
public class PleinEcranHistoriqueTests
{
    [Fact]
    public void Echap_quitte_d_abord_le_plein_ecran_puis_ferme()
    {
        Assert.Equal(ActionEchap.QuitterPleinEcran, PleinEcranHistorique.Echap(true));
        Assert.Equal(ActionEchap.Fermer, PleinEcranHistorique.Echap(false));
    }

    [Theory]
    // 100 % : identité.
    [InlineData(0, 0, 1920, 1080, 1.0, 1.0, 0.0, 0.0, 1920.0, 1080.0)]
    // 125 % : facteur 0,8.
    [InlineData(0, 0, 1920, 1080, 0.8, 0.8, 0.0, 0.0, 1536.0, 864.0)]
    // 150 % : facteur 2/3.
    [InlineData(0, 0, 2560, 1440, 2.0 / 3, 2.0 / 3, 0.0, 0.0, 1706.67, 960.0)]
    // Moniteur à gauche du primaire : origine négative.
    [InlineData(-1920, 0, 0, 1080, 1.0, 1.0, -1920.0, 0.0, 1920.0, 1080.0)]
    // Secondaire à droite, 125 %.
    [InlineData(1920, 0, 4480, 1440, 0.8, 0.8, 1536.0, 0.0, 2048.0, 1152.0)]
    public void Les_bornes_du_moniteur_passent_en_DIP(
        int gauche, int haut, int droite, int bas, double m11, double m22,
        double xAttendu, double yAttendu, double lAttendu, double hAttendu)
    {
        var r = PleinEcranHistorique.BornesDip(gauche, haut, droite, bas, m11, m22);

        Assert.Equal(xAttendu, r.Gauche, 0.01);
        Assert.Equal(yAttendu, r.Haut, 0.01);
        Assert.Equal(lAttendu, r.Largeur, 0.01);
        Assert.Equal(hAttendu, r.Hauteur, 0.01);
    }

    [Fact]
    public void Le_calcul_du_plein_ecran_ne_depend_d_aucun_type_WPF()
    {
        var fichier = Path.Combine(GardesPerimetreTests.CheminSources(), "Placement", "PleinEcranHistorique.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette : on exige la décision d'Échap.
        Assert.Contains("public static ActionEchap Echap(bool estPleinEcran)", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows", texte, StringComparison.Ordinal);
    }
}
