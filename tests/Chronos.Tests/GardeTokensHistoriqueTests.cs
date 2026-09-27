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
///   <item>la fusion du dictionnaire oubliée dans la fenêtre de réglages : ses <c>StaticResource</c> ne
///         résolvent plus sans <c>Application</c> (les smoke tests rougiraient, mais tard et de loin) ;</item>
///   <item>un token de taille réécrit (150 → 148) : les hauteurs §2.2 / §2.3 du plan de design sont le
///         CONTRAT que 34-04 à 34-07 consomment par <c>{StaticResource …}</c>, sans jamais taper un chiffre.</item>
/// </list>
///
/// Lecture XML du fichier source (pas de BAML, pas de thread STA) : ces tests sont des faits purs, sans STA.
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

    /// <summary>Les 45 tokens de taille (D-34-02, puis D-35-14 : +7 pour la vue 4 semaines) : corps §2, hauteurs de pistes
    /// §2.2 / §2.3 / §2.4, épaisseurs, opacités, rayon, tailles de fenêtre et gabarits de mise en page.</summary>
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
        // Vue 4 semaines (DESIGN_PLAN §2.4, frame E, D-35-14) : opacités des fantômes S-1 / S-2 / S-3, hauteur NIVEAU, colonne
        // des étiquettes, rangée de couverture et pas haut-à-haut entre deux rangées.
        ("HistoOpaciteSemaine1", 0.8),
        ("HistoOpaciteSemaine2", 0.45),
        ("HistoOpaciteSemaine3", 0.25),
        ("HistoHauteurNiveauQuatreSemaines", 250),
        ("HistoLargeurEtiquettesSemaines", 140),
        ("HistoHauteurCouvertureSemaine", 10),
        ("HistoPasCouvertureSemaines", 16),
    };

    private static string Racine()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    private static string CheminDesignTokens() => Path.Combine(Racine(), "Resources", "DesignTokens.xaml");
    // Réglages v2 (quick 260927) : la fenêtre de réglages est désormais Views/Reglages/ReglagesWindow.xaml.
    private static string CheminFenetreReglages() => Path.Combine(Racine(), "Views", "Reglages", "ReglagesWindow.xaml");

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
        var reglages = Charger(CheminFenetreReglages());

        foreach (var (cle, couleur) in PalettePromue)
        {
            var brosse = BrosseUnie(tokens, cle);
            Assert.True(brosse is not null, $"DesignTokens.xaml : la brosse « {cle} » (promue de SettingsWindow) manque.");
            var reelle = brosse!.Attribute("Color")?.Value;
            Assert.True(string.Equals(couleur, reelle, StringComparison.OrdinalIgnoreCase),
                $"DesignTokens.xaml : « {cle} » vaut {reelle ?? "(absent)"} au lieu de {couleur} — la promotion ne doit changer AUCUNE valeur.");

            Assert.True(BrosseUnie(reglages, cle) is null,
                $"ReglagesWindow.xaml déclare localement la brosse « {cle} » : elle doit venir du dictionnaire fusionné.");
        }

        // La fusion au niveau fenêtre (motif MainWindow.xaml) : sans elle, les StaticResource ne résolvent pas sans Application.
        var fusion = reglages.Descendants(Xaml + "ResourceDictionary")
            .Count(e => e.Attribute("Source")?.Value == PackUriDesignTokens);
        Assert.True(fusion == 1,
            $"ReglagesWindow.xaml doit fusionner exactement une fois « {PackUriDesignTokens} » (trouvé {fusion}).");
    }

    // Réglages v2 (quick 260927) : « SettingsWindow garde ses douze littéraux » est retiré avec l'ancienne fenêtre. La nouvelle
    // n'en garde AUCUN : GardeTokensReglagesTests.Aucune_couleur_ni_taille_en_dur_dans_la_fenetre_de_reglages le prouve.

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

        // Réglages v2 (quick 260927) : les tokens de la fenêtre de réglages sont comptés avec ceux de l'historique — la table
        // contractuelle des réglages vit dans GardeTokensReglagesTests (même règle : aucun token non contractuel).
        var attendus = TaillesHisto.Length + GardeTokensReglagesTests.TaillesReglages.Length;
        Assert.True(doubles.Count == attendus,
            $"DesignTokens.xaml : {doubles.Count} sys:Double au lieu de {attendus} — un token de taille non contractuel a été ajouté ou retiré.");

        // Le seul token non-double : l'épaisseur du bord de redimensionnement (WindowChrome.ResizeBorderThickness en 34-05).
        var bord = tokens.Descendants(Xaml + "Thickness").SingleOrDefault(e => Cle(e) == "HistoBordRedimensionnement");
        Assert.True(bord is not null, "DesignTokens.xaml : le Thickness « HistoBordRedimensionnement » manque.");
        Assert.Equal("6", bord!.Value.Trim());
    }

    // ------------------------------------------------------------------------------------------------
    // Task 2 — garde « aucune valeur en dur » sur la fenêtre Historique
    // ------------------------------------------------------------------------------------------------

    /// <summary>Couleur hexadécimale ou taille écrite en chiffres dans un XAML de l'historique : tout passe par
    /// <c>{StaticResource Histo…}</c>. (<c>Viewport="0,0,4,4"</c> n'existe que dans <c>DesignTokens.xaml</c>, hors périmètre.)
    /// 34-08 : les DP numériques NOMMÉES des pistes (<c>OpaciteTrou</c>, <c>EpaisseurFine</c>, <c>LongueurTiretReset</c>, <c>Plafond</c>,
    /// <c>NbBandes</c>…) sont aussi couvertes — la mutation n4 de 34-06 (<c>OpaciteTrou="0.5"</c>) passait sous l'ancienne garde. Les
    /// ressources locales nommées des vues (<c>&lt;sys:Double x:Key="HistoDecalageInfobulle"&gt;8&lt;/sys:Double&gt;</c>) ne sont pas des
    /// attributs : elles restent permises (motif 34-05).</summary>
    private static readonly Regex InterditXaml = new(
        "#[0-9A-Fa-f]{6,8}|(FontSize|Height|MinHeight|StrokeThickness|Thickness|Opacity|Opacite\\w*|Epaisseur\\w*|Longueur\\w*|Plafond|NbBandes)=\"[0-9]",
        RegexOptions.Compiled);

    /// <summary>Pinceau ou couleur fabriqués en C# dans une piste : la piste reçoit ses brosses par DP liée à un token.
    /// <c>Brushes.Transparent</c> est le SEUL pinceau nommé toléré (défaut « ne rien dessiner »).</summary>
    private static readonly Regex InterditCs = new(
        @"Frozen\(0x|Color\.FromRgb\(|Color\.FromArgb\(|Colors\.[A-Z]|Brushes\.(?!Transparent\b)[A-Z]",
        RegexOptions.Compiled);

    [Fact]
    public void Aucune_couleur_ni_taille_en_dur_dans_les_vues_et_les_pistes_de_l_historique()
    {
        var racine = Racine();
        var vues = Path.Combine(racine, "Views", "Historique");
        var pistes = Path.Combine(racine, "Controls", "Historique");

        var xamls = Directory.Exists(vues) ? Directory.EnumerateFiles(vues, "*.xaml", SearchOption.AllDirectories).ToList() : new List<string>();
        var cs = Directory.Exists(pistes) ? Directory.EnumerateFiles(pistes, "*.cs", SearchOption.AllDirectories).ToList() : new List<string>();

        // Anti-mutisme (34-08) : une garde qui ne lit rien passerait toujours — la fenêtre, les deux vues et les sept pistes.
        Assert.True(xamls.Count >= 3, $"la garde ne voit pas les XAML de la fenêtre Historique ({xamls.Count}, attendu >= 3) : {vues}");
        Assert.True(cs.Count >= 7, $"la garde ne voit pas les pistes de l'historique ({cs.Count}, attendu >= 7) : {pistes}");

        var infractions = new List<string>();
        foreach (var (fichiers, motif) in new[] { (xamls, InterditXaml), (cs, InterditCs) })
        {
            foreach (var fichier in fichiers)
            {
                var texte = File.ReadAllText(fichier);
                foreach (Match m in motif.Matches(texte))
                {
                    var ligne = texte.Take(m.Index).Count(c => c == '\n') + 1;
                    var debut = texte.LastIndexOf('\n', m.Index) + 1;
                    var fin = texte.IndexOf('\n', m.Index);
                    var extrait = texte[debut..(fin < 0 ? texte.Length : fin)].Trim();
                    infractions.Add($"{Path.GetRelativePath(racine, fichier)}:{ligne}: {extrait}");
                }
            }
        }

        Assert.True(infractions.Count == 0,
            "HIS-07 : une couleur ou une taille est écrite EN DUR dans la fenêtre Historique — la source unique est "
            + "Resources/DesignTokens.xaml ({StaticResource Histo…}), jamais un chiffre ni un hexadécimal dans une vue ou une piste.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>Un texte VISIBLE écrit en dur dans un XAML de l'historique (<c>Text="100 %"</c>) : les mots et repères viennent de
    /// <c>TextesHistorique</c> (source unique, D-34-35) par <c>{x:Static}</c> ou par binding au VM. Tolérés, nommés : la marque
    /// « Chronos » de l'en-tête et les trois glyphes de boutons (✕ ‹ ›), qui ne sont pas des mots.</summary>
    private static readonly Regex TexteLitteral = new(@"\b(Text|Content|ToolTip)=""(?!\{)([^""]*)""", RegexOptions.Compiled);
    private static readonly HashSet<string> TextesToleres = new(StringComparer.Ordinal) { "Chronos", "✕", "‹", "›" };

    [Fact]
    public void Aucun_texte_visible_ecrit_en_dur_dans_les_xaml_de_l_historique()
    {
        var racine = Racine();
        var xamls = Directory.EnumerateFiles(Path.Combine(racine, "Views", "Historique"), "*.xaml", SearchOption.AllDirectories).ToList();
        Assert.True(xamls.Count >= 3, $"la garde ne voit pas les XAML de la fenêtre Historique : {xamls.Count}");

        var infractions = new List<string>();
        foreach (var fichier in xamls)
        {
            var lignes = File.ReadAllLines(fichier);
            for (var i = 0; i < lignes.Length; i++)
                foreach (Match m in TexteLitteral.Matches(lignes[i]))
                    if (!TextesToleres.Contains(m.Groups[2].Value))
                        infractions.Add($"{Path.GetRelativePath(racine, fichier)}:{i + 1}: {lignes[i].Trim()}");
        }

        Assert.True(infractions.Count == 0,
            "HIS-06 : un texte visible est écrit EN DUR dans la fenêtre Historique — la source unique est TextesHistorique "
            + "({x:Static txt:TextesHistorique.…} ou un binding au VM).\n  " + string.Join("\n  ", infractions));
    }
}

/// <summary>
/// Le dictionnaire des tokens se charge SEUL (sans <c>Application</c>), comme le fait <c>ReglagesWindow</c> par sa fusion
/// pack URI : un <c>sys:Double</c> mal typé, un <c>StaticResource HistoGris</c> déclaré avant sa cible ou un
/// <c>BoolToVis</c> en double ne se voient qu'au chargement BAML — pas dans l'XML. Classe séparée : charge du BAML,
/// donc collection sérialisée (voir <see cref="XamlWpfCollection"/>).
/// </summary>
[Collection("XAML WPF")]
public class GardeTokensHistoriqueXamlTests
{
    [WpfFact]
    public void Le_dictionnaire_de_tokens_se_charge_seul_et_par_la_fenetre_des_reglages()
    {
        // Variante retenue : Application.LoadComponent avec l'URI relative « ;component » (voir le SUMMARY 34-01).
        var dict = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/Chronos;component/Resources/DesignTokens.xaml", UriKind.Relative));

        Assert.True(dict["HistoGris"] is System.Windows.Media.SolidColorBrush gris
                    && gris.Color == System.Windows.Media.Color.FromRgb(0x5A, 0x59, 0x60),
            "HistoGris doit être une SolidColorBrush #5A5960 une fois le BAML chargé.");
        Assert.True(dict["HistoHauteurNiveauPistes"] is double hauteur && hauteur == 150,
            "HistoHauteurNiveauPistes doit être un double boxé (150), pas une chaîne.");
        Assert.True(dict["HistoBordRedimensionnement"] is System.Windows.Thickness bord && bord.Left == 6,
            "HistoBordRedimensionnement doit être un Thickness de 6.");
        Assert.True(dict["HistoHachure"] is System.Windows.Media.DrawingBrush,
            "HistoHachure doit être un DrawingBrush.");

        // Les sept brosses promues résolvent aussi par le dictionnaire (c'est ce que ReglagesWindow fusionne).
        Assert.True(dict["Panel"] is System.Windows.Media.SolidColorBrush panel
                    && panel.Color == System.Windows.Media.Color.FromRgb(0x15, 0x13, 0x22));
    }
}
