using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Gardes des TOKENS de la fenêtre Historique (HIS-07, plan 34-01) : une seule source de vérité pour les
/// couleurs et les tailles, AVANT qu'une seule vue existe.
///
/// Ce que ces gardes attrapent et que <c>dotnet build</c> ne voit pas :
/// <list type="bullet">
///   <item>la palette du chrome des réglages (<c>Panel/Panel2/Line/Ink/Ink2/Accent/Ok</c>) promue dans
///         <c>DesignTokens.xaml</c> avec une valeur qui bouge d'un caractère — la fenêtre de réglages
///         changerait de teinte en silence ;</item>
///   <item>la fusion du dictionnaire oubliée dans <c>SettingsWindow.xaml</c> : ses <c>StaticResource</c> ne
///         résolvent plus sans <c>Application</c> (les smoke tests rougiraient, mais tard et de loin) ;</item>
///   <item>un token de taille réécrit (150 → 148) : les hauteurs §2.2 / §2.3 du plan de design sont le
///         CONTRAT que 34-04 à 34-07 consomment par <c>{StaticResource …}</c>, sans jamais taper un chiffre.</item>
/// </list>
///
/// Lecture XML du fichier source (pas de BAML, pas de thread STA) : ces tests sont des <c>[Fact]</c> purs.
/// </summary>
public class GardeTokensHistoriqueTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Sys = "clr-namespace:System;assembly=mscorlib";

    private const string PackUriDesignTokens = "pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml";

    /// <summary>Les sept brosses du chrome, avec les valeurs EXACTES qu'elles avaient dans <c>SettingsWindow.xaml</c>
    /// avant la promotion (relevées le 2026-09-27, HEAD 47904ff).</summary>
    private static readonly (string Cle, string Couleur)[] PalettePromue =
    {
        ("Panel",  "#151322"),
        ("Panel2", "#1E1B30"),
        ("Line",   "#2C2942"),
        ("Ink",    "#F2F0FB"),
        ("Ink2",   "#A9A6C4"),
        ("Accent", "#8B7BF0"),
        ("Ok",     "#4EC98A"),
    };

    /// <summary>Les neutres de la fenêtre Historique (DESIGN_PLAN §2 et §2.0 ; <c>HistoGris</c> = D-34-01).</summary>
    private static readonly (string Cle, string Couleur)[] CouleursHisto =
    {
        ("HistoTokens",    "#8C89A8"),
        ("HistoSousAgent", "#5A5776"),
        ("HistoModele1",   "#9D9AB8"),
        ("HistoModele2",   "#6E6B8C"),
        ("HistoModele3",   "#4A4762"),
        ("HistoGris",      "#5A5960"),
    };

    /// <summary>Les 38 tokens de taille (D-34-02) : corps §2, hauteurs de pistes §2.2 / §2.3, épaisseurs, opacités,
    /// rayon, tailles de fenêtre et gabarits de mise en page.</summary>
    private static readonly (string Cle, double Valeur)[] TaillesHisto =
    {
        ("HistoCorpsTitre", 16),
        ("HistoCorpsGrand", 14),
        ("HistoCorpsNormal", 11.5),
        ("HistoCorpsMoyen", 11),
        ("HistoCorpsPetit", 10.5),
        ("HistoCorpsMini", 9.5),
        ("HistoCorpsLegende", 9),
        ("HistoCorpsInfime", 8.5),
        ("HistoEpaisseurEscalier", 2.2),
        ("HistoEpaisseurPremierPlan", 2.4),
        ("HistoEpaisseurFin", 1),
        ("HistoEpaisseurBordureActive", 1.5),
        ("HistoLongueurTiretReset", 8),
        ("HistoOpaciteTrou", 0.35),
        ("HistoOpaciteCouverture", 0.55),
        ("HistoOpaciteMaintenant", 0.6),
        ("HistoRayonCoins", 16),
        ("HistoLargeurLibelles", 96),
        ("HistoLargeurLegendeDroite", 72),
        ("HistoHauteurNiveauPistes", 150),
        ("HistoHauteurRythmePistes", 72),
        ("HistoHauteurTokensPistes", 72),
        ("HistoHauteurCouverture", 12),
        ("HistoHauteurNiveauSimplifie", 200),
        ("HistoHauteurTokensSimplifie", 90),
        ("HistoHauteurNiveauTuiles", 120),
        ("HistoHauteurFenetres5hTuiles", 62),
        ("HistoHauteurRythmeTuiles", 58),
        ("HistoHauteurTokensTuiles", 58),
        ("HistoHauteurNiveauJour", 190),
        ("HistoHauteurRythmeJour", 64),
        ("HistoHauteurTokensJour", 64),
        ("HistoLargeurMin", 760),
        ("HistoHauteurMin", 480),
        ("HistoLargeurDefaut", 920),
        ("HistoHauteurDefaut", 610),
        ("HistoHauteurAnnotations", 18),
        ("HistoHauteurEnTete", 92),
    };

    private static string Racine()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    private static string CheminDesignTokens() => Path.Combine(Racine(), "Resources", "DesignTokens.xaml");
    private static string CheminSettingsWindow() => Path.Combine(Racine(), "Views", "SettingsWindow.xaml");

    private static XDocument Charger(string chemin)
    {
        Assert.True(File.Exists(chemin), $"Fichier introuvable : {chemin}");
        return XDocument.Load(chemin);
    }

    private static string? Cle(XElement e) => e.Attribute(X + "Key")?.Value;

    private static XElement? BrosseUnie(XDocument doc, string cle)
        => doc.Descendants(Xaml + "SolidColorBrush").SingleOrDefault(e => Cle(e) == cle);

    // ------------------------------------------------------------------------------------------------
    // Task 1 — promotion de la palette, tokens Histo*, tailles sys:Double
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void La_palette_des_reglages_est_promue_sans_changer_une_valeur()
    {
        var tokens = Charger(CheminDesignTokens());
        var reglages = Charger(CheminSettingsWindow());

        foreach (var (cle, couleur) in PalettePromue)
        {
            var brosse = BrosseUnie(tokens, cle);
            Assert.True(brosse is not null, $"DesignTokens.xaml : la brosse « {cle} » (promue de SettingsWindow) manque.");
            var reelle = brosse!.Attribute("Color")?.Value;
            Assert.True(string.Equals(couleur, reelle, StringComparison.OrdinalIgnoreCase),
                $"DesignTokens.xaml : « {cle} » vaut {reelle ?? "(absent)"} au lieu de {couleur} — la promotion ne doit changer AUCUNE valeur.");

            Assert.True(BrosseUnie(reglages, cle) is null,
                $"SettingsWindow.xaml déclare encore localement la brosse « {cle} » : elle doit venir du dictionnaire fusionné.");
        }

        // La fusion au niveau fenêtre (motif MainWindow.xaml) : sans elle, les StaticResource ne résolvent pas sans Application.
        var fusion = reglages.Descendants(Xaml + "ResourceDictionary")
            .Count(e => e.Attribute("Source")?.Value == PackUriDesignTokens);
        Assert.True(fusion == 1,
            $"SettingsWindow.xaml doit fusionner exactement une fois « {PackUriDesignTokens} » (trouvé {fusion}).");
    }

    [Fact]
    public void SettingsWindow_garde_ses_douze_litteraux_et_pas_un_de_plus()
    {
        var texte = File.ReadAllText(CheminSettingsWindow());
        var litteraux = Regex.Matches(texte, "#[0-9A-Fa-f]{6,8}").Select(m => m.Value.ToUpperInvariant()).ToList();

        // 19 avant la promotion − 7 promues = 12 : aucune valeur ajoutée ni retirée hors des sept.
        Assert.True(litteraux.Count == 12,
            $"SettingsWindow.xaml contient {litteraux.Count} littéraux hexadécimaux au lieu de 12 : "
            + string.Join(", ", litteraux));

        foreach (var (cle, couleur) in PalettePromue)
        {
            var occurrences = litteraux.Count(l => l == couleur.ToUpperInvariant());
            // #2C2942 (Line) reste UNE fois : la piste du Switch, hors du périmètre de la promotion.
            var tolere = cle == "Line" ? 1 : 0;
            Assert.True(occurrences == tolere,
                $"SettingsWindow.xaml : l'hexadécimal {couleur} ({cle}) apparaît {occurrences} fois, attendu {tolere}.");
        }
    }

    [Fact]
    public void Les_tokens_couleur_de_l_historique_existent_avec_leurs_valeurs()
    {
        var tokens = Charger(CheminDesignTokens());

        foreach (var (cle, couleur) in CouleursHisto)
        {
            var brosse = BrosseUnie(tokens, cle);
            Assert.True(brosse is not null, $"DesignTokens.xaml : le token « {cle} » manque.");
            var reelle = brosse!.Attribute("Color")?.Value;
            Assert.True(string.Equals(couleur, reelle, StringComparison.OrdinalIgnoreCase),
                $"DesignTokens.xaml : « {cle} » vaut {reelle ?? "(absent)"} au lieu de {couleur} (DESIGN_PLAN §2 / §2.0).");
        }

        // La hachure « hors couverture » est un token de brosse (D-34-04) : tuilée 4 × 4, trait HistoGris, aucun hexadécimal.
        var hachure = tokens.Descendants(Xaml + "DrawingBrush").SingleOrDefault(e => Cle(e) == "HistoHachure");
        Assert.True(hachure is not null, "DesignTokens.xaml : le DrawingBrush « HistoHachure » manque.");
        Assert.Equal("Tile", hachure!.Attribute("TileMode")?.Value);
        Assert.Equal("0,0,4,4", hachure.Attribute("Viewport")?.Value);
        Assert.Equal("Absolute", hachure.Attribute("ViewportUnits")?.Value);
        var plume = hachure.Descendants(Xaml + "Pen").SingleOrDefault();
        Assert.True(plume is not null, "HistoHachure : le Pen manque.");
        Assert.Equal("{StaticResource HistoGris}", plume!.Attribute("Brush")?.Value);
        Assert.DoesNotMatch("#[0-9A-Fa-f]{6,8}", hachure.ToString());
    }

    [Fact]
    public void Les_tokens_de_taille_sont_des_doubles_nommes()
    {
        var tokens = Charger(CheminDesignTokens());
        var doubles = tokens.Descendants(Sys + "Double").ToList();

        foreach (var (cle, valeur) in TaillesHisto)
        {
            var element = doubles.SingleOrDefault(e => Cle(e) == cle);
            Assert.True(element is not null, $"DesignTokens.xaml : le sys:Double « {cle} » manque.");
            Assert.True(double.TryParse(element!.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var reelle),
                $"« {cle} » : valeur « {element.Value} » non numérique (écrire avec un POINT, le parseur XAML est invariant).");
            Assert.True(reelle == valeur, $"« {cle} » vaut {reelle.ToString(CultureInfo.InvariantCulture)} au lieu de {valeur.ToString(CultureInfo.InvariantCulture)}.");
        }

        Assert.True(doubles.Count == TaillesHisto.Length,
            $"DesignTokens.xaml : {doubles.Count} sys:Double au lieu de {TaillesHisto.Length} — un token de taille non contractuel a été ajouté ou retiré.");

        // Le seul token non-double : l'épaisseur du bord de redimensionnement (WindowChrome.ResizeBorderThickness en 34-05).
        var bord = tokens.Descendants(Xaml + "Thickness").SingleOrDefault(e => Cle(e) == "HistoBordRedimensionnement");
        Assert.True(bord is not null, "DesignTokens.xaml : le Thickness « HistoBordRedimensionnement » manque.");
        Assert.Equal("6", bord!.Value.Trim());
    }
}
