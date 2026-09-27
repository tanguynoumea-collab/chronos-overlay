using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Quick 260927-reglages-v2 — les TOKENS de la fenêtre de réglages (DESIGN_PLAN_REGLAGES §2) : une seule source de vérité pour
/// ses tailles et pour la seule couleur ajoutée (<c>Danger</c>, action destructive). Lecture XML du dictionnaire (faits purs, sans
/// STA). Le décompte global des <c>sys:Double</c> est tenu par <see cref="GardeTokensHistoriqueTests"/>, qui additionne les deux
/// tables : un token de réglages ajouté ici sans être déclaré dans <see cref="TaillesReglages"/> fait rougir les deux gardes.
/// </summary>
public class GardeTokensReglagesTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Sys = "clr-namespace:System;assembly=mscorlib";

    /// <summary>Les tailles contractuelles de la fenêtre de réglages : fenêtre (§2), rail, colonne de lecture, typographie
    /// (titre de section 18 · titre de carte 13 · corps/aide 11 · libellé du rail 12), gabarits conservés de l'ancienne fenêtre
    /// (interrupteur, vignette de thème) ramenés sur la grille de 4 px, aperçus, opacité de l'état désactivé.</summary>
    internal static readonly (string Cle, double Valeur)[] TaillesReglages =
    {
        ("ReglagesLargeurMin", 640),
        ("ReglagesHauteurMin", 440),
        ("ReglagesLargeurDefaut", 860),
        ("ReglagesHauteurDefaut", 580),
        ("ReglagesRailLargeur", 208),
        ("ReglagesContenuMax", 640),
        ("ReglagesHauteurBarreTitre", 40),
        ("ReglagesLargeurBoutonFenetre", 44),
        ("ReglagesHauteurEntreeRail", 36),
        ("ReglagesCorpsTitreSection", 18),
        ("ReglagesCorpsTitreCarte", 13),
        ("ReglagesCorpsAide", 11),
        ("ReglagesCorpsRail", 12),
        ("ReglagesCorpsEtiquette", 10.5),
        ("ReglagesCorpsMini", 9.5),
        ("ReglagesInterrupteurLargeur", 40),
        ("ReglagesInterrupteurHauteur", 24),
        ("ReglagesInterrupteurBouton", 16),
        ("ReglagesVignetteLargeur", 88),
        ("ReglagesVignetteHauteur", 76),
        ("ReglagesVignetteDisque", 32),
        ("ReglagesVignettePastille", 8),
        ("ReglagesApercuCadran", 144),
        ("ReglagesApercuSessionsHauteur", 120),
        ("ReglagesLogo", 16),
        ("ReglagesLogoTrait", 3),
        ("ReglagesTraitSelection", 3),
        ("ReglagesPastille", 10),
        ("ReglagesBarreDefilement", 8),
        ("ReglagesOpaciteInactif", 0.45),
    };

    private static string Racine()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    private static XDocument Tokens()
    {
        var chemin = Path.Combine(Racine(), "Resources", "DesignTokens.xaml");
        Assert.True(File.Exists(chemin), $"Fichier introuvable : {chemin}");
        return XDocument.Load(chemin);
    }

    [Fact]
    public void Les_tokens_de_taille_des_reglages_sont_des_doubles_nommes()
    {
        var doubles = Tokens().Descendants(Sys + "Double").ToList();

        foreach (var (cle, valeur) in TaillesReglages)
        {
            var element = doubles.SingleOrDefault(e => e.Attribute(X + "Key")?.Value == cle);
            Assert.True(element is not null, $"DesignTokens.xaml : le sys:Double « {cle} » manque.");
            Assert.True(double.TryParse(element!.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var reelle),
                $"« {cle} » : valeur « {element.Value} » non numérique (écrire avec un POINT).");
            Assert.True(reelle == valeur, $"« {cle} » vaut {reelle.ToString(CultureInfo.InvariantCulture)} au lieu de {valeur.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    /// <summary>§2 : <c>Danger</c> reprend la valeur de l'ancien « Quitter » (#E8907F), inchangée — c'est la seule couleur ajoutée.</summary>
    [Fact]
    public void Danger_est_un_token_de_la_valeur_de_l_ancien_Quitter()
    {
        var danger = Tokens().Descendants(Xaml + "SolidColorBrush").SingleOrDefault(e => e.Attribute(X + "Key")?.Value == "Danger");
        Assert.True(danger is not null, "DesignTokens.xaml : la brosse « Danger » manque.");
        Assert.Equal("#E8907F", danger!.Attribute("Color")?.Value, StringComparer.OrdinalIgnoreCase);
    }
}
