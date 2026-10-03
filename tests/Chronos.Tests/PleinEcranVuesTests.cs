using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Services.Historique;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-10 (phase 38) — le plein écran de l'Historique est un ÉCHANGE DE DICTIONNAIRE : <see cref="DictionnairePleinEcran"/> construit,
/// depuis les tokens <c>*PleinEcran</c> de <c>DesignTokens.xaml</c>, un dictionnaire dont les clés sont les clés normales. Ces tests
/// vérifient (1) son contenu contre la table du contrat DESIGN_PLAN_CYCLE2 § 4.2, écrite EN DUR ici, et (2) son effet sur l'arbre visuel
/// réel des vues : proportions des pistes, plafonds, couverture fixe, plus de défilement, corps et traits agrandis, retour exact au normal.
/// Collection sérialisée obligatoire (chargeur BAML).
/// </summary>
[Collection("XAML WPF")]
public class PleinEcranVuesTests
{
    /// <summary>Le dictionnaire des tokens, chargé seul (sans Application).</summary>
    internal static ResourceDictionary Tokens()
        => (ResourceDictionary)Application.LoadComponent(new Uri("/Chronos;component/Resources/DesignTokens.xaml", UriKind.Relative));

    // ------------------------------------------------------------------ Contenu du dictionnaire (contrat § 4.2)

    /// <summary>Table du contrat § 4.2 (+ en-tête 124, écart consigné) : clé normale → valeur plein écran.</summary>
    private static readonly (string Cle, double Valeur)[] ValeursContrat =
    {
        ("HistoCorpsInfime", 11.5),
        ("HistoCorpsLegende", 12),
        ("HistoCorpsMini", 13),
        ("HistoCorpsPetit", 14),
        ("HistoCorpsMoyen", 15),
        ("HistoCorpsNormal", 15.5),
        ("HistoCorpsGrand", 18),
        ("HistoCorpsTitre", 21),
        ("HistoLargeurLibelles", 128),
        ("HistoLargeurLegendeDroite", 96),
        ("HistoEpaisseurEscalier", 3),
        ("HistoEpaisseurPremierPlan", 3.2),
        ("HistoLongueurTiretReset", 11),
        ("HistoHauteurEnTete", 124),
        ("HistoPlafondNiveau", 520),
        ("HistoPlafondPiste", 240),
    };

    /// <summary>Rangées étoilées : l'étoile reprend la hauteur normale de la piste (§ 2.2 / 2.3 / 2.4).</summary>
    private static readonly (string Rangee, string Hauteur, double Etoiles)[] RangeesContrat =
    {
        ("HistoRangeeNiveauSemaine", "HistoHauteurNiveauPistes", 150),
        ("HistoRangeeRythmeSemaine", "HistoHauteurRythmePistes", 72),
        ("HistoRangeeTokensSemaine", "HistoHauteurTokensPistes", 72),
        ("HistoRangeeNiveauJour", "HistoHauteurNiveauJour", 190),
        ("HistoRangeeRythmeJour", "HistoHauteurRythmeJour", 64),
        ("HistoRangeeTokensJour", "HistoHauteurTokensJour", 64),
        ("HistoRangeeNiveauQuatreSemaines", "HistoHauteurNiveauQuatreSemaines", 250),
    };

    [WpfFact]
    public void Le_dictionnaire_plein_ecran_porte_les_valeurs_du_contrat()
    {
        var d = DictionnairePleinEcran.Construire(Tokens());

        foreach (var (cle, valeur) in ValeursContrat)
            Assert.True(d[cle] is double v && v == valeur, $"PleinEcran[« {cle} »] = {d[cle]} au lieu de {valeur} (contrat § 4.2).");

        foreach (var (rangee, hauteur, etoiles) in RangeesContrat)
        {
            Assert.Equal(new GridLength(etoiles, GridUnitType.Star), Assert.IsType<GridLength>(d[rangee]));
            Assert.True(d[hauteur] is double h && double.IsNaN(h), $"PleinEcran[« {hauteur} »] doit valoir NaN (la piste s'étire dans sa rangée).");
        }

        Assert.Equal(ScrollBarVisibility.Disabled, Assert.IsType<ScrollBarVisibility>(d["HistoDefilementVertical"]));
        Assert.Equal(16 + 7 * 2 + 1, d.Count);
    }
}
