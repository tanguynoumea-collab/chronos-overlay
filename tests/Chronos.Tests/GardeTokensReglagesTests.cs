using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
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

    // ------------------------------------------------------------------ Fenêtre : aucune valeur en dur, une seule fusion, vocabulaire

    private static string DossierReglages() => Path.Combine(Racine(), "Views", "Reglages");

    private static List<string> XamlDesReglages()
    {
        var xamls = Directory.Exists(DossierReglages())
            ? Directory.EnumerateFiles(DossierReglages(), "*.xaml", SearchOption.AllDirectories).ToList()
            : new List<string>();
        Assert.True(xamls.Count >= 1, $"la garde ne voit pas la fenêtre de réglages : {DossierReglages()}");   // anti-mutisme
        Assert.Contains(xamls, x => Path.GetFileName(x) == "ReglagesWindow.xaml");
        return xamls;
    }

    /// <summary>Une couleur ou une taille écrite en chiffres : tout passe par <c>{StaticResource …}</c> (DesignTokens.xaml pour les
    /// tailles et les couleurs ; ressources locales NOMMÉES pour les épaisseurs et les rayons, motif 34-05). Les marges et
    /// rembourrages (grille de 4 px) restent littéraux, comme dans la fenêtre Historique.</summary>
    private static readonly Regex InterditXaml = new(
        @"#[0-9A-Fa-f]{6,8}|\b(FontSize|Width|Height|MinWidth|MinHeight|MaxWidth|MaxHeight|StrokeThickness|BorderThickness|Opacity|CornerRadius)=""[0-9.]",
        RegexOptions.Compiled);

    [Fact]
    public void Aucune_couleur_ni_taille_en_dur_dans_la_fenetre_de_reglages()
    {
        var infractions = new List<string>();
        foreach (var fichier in XamlDesReglages())
        {
            var lignes = File.ReadAllLines(fichier);
            for (var i = 0; i < lignes.Length; i++)
                foreach (Match m in InterditXaml.Matches(lignes[i]))
                    infractions.Add($"{Path.GetFileName(fichier)}:{i + 1}: {m.Value} — {lignes[i].Trim()}");
        }

        Assert.True(infractions.Count == 0,
            "Réglages v2 : une couleur ou une taille est écrite EN DUR dans la fenêtre de réglages — la source unique est "
            + "Resources/DesignTokens.xaml.\n  " + string.Join("\n  ", infractions));
    }

    /// <summary>La palette du chrome (34-01) est FUSIONNÉE une fois au niveau fenêtre — les StaticResource résolvent sans
    /// Application — et aucune brosse n'est redéclarée localement (elle changerait de teinte en silence).</summary>
    [Fact]
    public void La_fenetre_fusionne_les_tokens_une_fois_et_ne_redeclare_aucune_brosse()
    {
        var doc = XDocument.Load(Path.Combine(DossierReglages(), "ReglagesWindow.xaml"));
        var fusion = doc.Descendants(Xaml + "ResourceDictionary")
            .Count(e => e.Attribute("Source")?.Value == "pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml");
        Assert.Equal(1, fusion);
        Assert.Empty(doc.Descendants(Xaml + "SolidColorBrush"));
    }

    /// <summary>La garde de vocabulaire de l'historique (34-08) étendue aux textes des réglages : aucun mot de projection.</summary>
    private static readonly Regex Projection = new(@"épuisé[e]? vers|à ce rythme|projection|prévision|estim(é|ation|er)|tendance|dans \d+ ?h\b",
                                                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static List<string> CodeDesReglages()
    {
        var fichiers = Directory.EnumerateFiles(DossierReglages(), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Append(Path.Combine(Racine(), "ViewModels", "ReglagesViewModel.cs"))
            .ToList();
        Assert.True(fichiers.Count >= 4, "la garde ne voit pas le code des réglages : " + fichiers.Count);
        return fichiers;
    }

    [Fact]
    public void Aucun_mot_de_projection_dans_les_textes_des_reglages()
    {
        var infractions = new List<string>();
        foreach (var fichier in CodeDesReglages())
        {
            var lignes = File.ReadAllLines(fichier);
            for (var i = 0; i < lignes.Length; i++)
                if (Projection.Match(lignes[i]) is { Success: true } m)
                    infractions.Add($"{Path.GetFileName(fichier)}:{i + 1}: « {m.Value} » — {lignes[i].Trim()}");
        }
        Assert.True(infractions.Count == 0, "un mot de projection est entré dans les réglages :\n  " + string.Join("\n  ", infractions));
    }

    /// <summary>§5 : le diagnostic vit dans la fenêtre — plus de boîte de message, et sa génération part sur le pool.</summary>
    [Fact]
    public void Le_diagnostic_n_ouvre_plus_de_boite_de_message_et_se_genere_sur_le_pool()
    {
        var fichiers = CodeDesReglages().Append(Path.Combine(Racine(), "ViewModels", "MainViewModel.cs")).ToList();
        foreach (var fichier in fichiers)
            Assert.DoesNotContain("MessageBox", File.ReadAllText(fichier), StringComparison.Ordinal);

        var vm = File.ReadAllText(Path.Combine(Racine(), "ViewModels", "MainViewModel.cs"));
        Assert.Contains("() => Task.Run(() => _diagnostic.BuildReportAsync())", vm, StringComparison.Ordinal);
    }

    /// <summary>L'ancienne fenêtre a disparu, et personne ne pose d'<c>Owner</c> sur les réglages : ni la vue du cadran, ni
    /// l'ouvreur, ni la fabrique de l'App (DESIGN_PLAN §4 : la fenêtre n'est plus possédée par le cadran topmost).</summary>
    [Fact]
    public void L_ancienne_fenetre_a_disparu_et_personne_ne_pose_d_Owner()
    {
        Assert.False(File.Exists(Path.Combine(Racine(), "Views", "SettingsWindow.xaml")), "SettingsWindow.xaml doit être supprimée");
        Assert.False(File.Exists(Path.Combine(Racine(), "Views", "SettingsWindow.xaml.cs")), "SettingsWindow.xaml.cs doit être supprimée");

        var cadran = File.ReadAllText(Path.Combine(Racine(), "Views", "MainWindow.xaml.cs"));
        Assert.DoesNotContain("Owner", cadran, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReglagesWindow", cadran, StringComparison.Ordinal);
        Assert.DoesNotContain("SettingsWindow", cadran, StringComparison.Ordinal);

        foreach (var cs in Directory.EnumerateFiles(DossierReglages(), "*.cs"))
            Assert.DoesNotMatch(@"\bOwner\s*=", File.ReadAllText(cs));

        var app = File.ReadAllLines(Path.Combine(Racine(), "App.xaml.cs"));
        var fabrique = Assert.Single(app, l => l.Contains("new ReglagesWindow(", StringComparison.Ordinal));
        Assert.Contains("OuvreurReglages", fabrique, StringComparison.Ordinal);
        Assert.DoesNotContain("Owner", fabrique, StringComparison.Ordinal);
    }
}
