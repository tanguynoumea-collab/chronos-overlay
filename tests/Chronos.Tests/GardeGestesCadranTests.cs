using System.IO;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GST-01 — garde TEXTUELLE du répartiteur unique de gestes (grille <c>Racine</c> de <c>MainWindow</c>). <c>ClickCount</c>
/// n'est pas réglable en test (setter interne) : la décision vit dans <c>AutomateGeste</c> et <c>ArbitreClicCentre</c>, testés
/// purs. On garde ici la FORME du code-behind : appui filtré par la silhouette puis confié à l'automate, relâchement qui ne
/// lit jamais <c>ClickCount</c>, glisser au seuil Windows suivi de l'accroche, perte de capture transmise, plus de
/// <c>CentreHit</c> ni de <c>DragMove</c> de fenêtre. Les tests CAD-02 (recalage, phase 40) sont conservés tels quels.
/// </summary>
public class GardeGestesCadranTests
{
    private static string Lire(params string[] morceaux)
    {
        var fichier = Path.Combine(new[] { GardesPerimetreTests.CheminSources() }.Concat(morceaux).ToArray());
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
        var texte = File.ReadAllText(fichier);
        Assert.True(texte.Length > 200, $"{fichier} est suspicieusement court : la garde ne lit pas le vrai fichier.");
        return texte;
    }

    /// <summary>Corps d'une méthode : de sa signature jusqu'à la première accolade fermante de méthode (fins de ligne normalisées).</summary>
    private static string Corps(string texte, string signature)
    {
        texte = texte.Replace("\r\n", "\n");
        var debut = texte.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(debut >= 0, $"{signature} introuvable dans MainWindow.xaml.cs");
        var fin = texte.IndexOf("\n    }", debut, StringComparison.Ordinal);
        Assert.True(fin > debut, $"fin de {signature} introuvable");
        return texte[debut..fin];
    }

    private static int Compter(string texte, string motif)
    {
        int n = 0, i = 0;
        while ((i = texte.IndexOf(motif, i, StringComparison.Ordinal)) >= 0) { n++; i += motif.Length; }
        return n;
    }

    [Fact]
    public void Le_repartiteur_unique_est_sur_la_grille_racine()
    {
        var xaml = Lire("Views", "MainWindow.xaml");
        Assert.Contains("x:Name=\"Racine\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonDown=\"Racine_MouseLeftButtonDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseMove=\"Racine_MouseMove\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonUp=\"Racine_MouseLeftButtonUp\"", xaml, StringComparison.Ordinal);
        Assert.Contains("LostMouseCapture=\"Racine_LostMouseCapture\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseRightButtonUp=\"OnRightClick\"", xaml, StringComparison.Ordinal);
        Assert.Equal(1, Compter(xaml, "MouseLeftButtonDown="));
        Assert.DoesNotContain("CentreHit", xaml, StringComparison.Ordinal);
        Assert.Equal(2, Compter(xaml, "Cursor=\"Hand\""));   // les deux Button de pastilles, et eux seuls

        var code = Lire("Views", "MainWindow.xaml.cs");
        Assert.DoesNotContain("MouseLeftButtonDown +=", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Cadran_MouseLeftButtonDown", code, StringComparison.Ordinal);
        Assert.DoesNotContain("CentreHit", code, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHandler(", code, StringComparison.Ordinal);
        Assert.DoesNotContain("handledEventsToo", code, StringComparison.Ordinal);
    }

    [Fact]
    public void L_appui_est_filtre_par_la_silhouette_et_confie_a_l_automate()
    {
        var corps = Corps(Lire("Views", "MainWindow.xaml.cs"), "private void Racine_MouseLeftButtonDown");
        Assert.Contains("ZoneGeste.Trouver(", corps, StringComparison.Ordinal);
        Assert.Contains("ZoneGeste.Contient(", corps, StringComparison.Ordinal);
        Assert.Contains("_geste.Appui(", corps, StringComparison.Ordinal);
        Assert.Contains("e.ClickCount", corps, StringComparison.Ordinal);
        Assert.Contains("ClicCentre(", corps, StringComparison.Ordinal);
        Assert.Contains("CaptureMouse()", corps, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleCenterMode()", corps, StringComparison.Ordinal);   // la bascule directe ferait double emploi
    }

    [Fact]
    public void Le_relachement_ne_lit_jamais_ClickCount()
    {
        var corps = Corps(Lire("Views", "MainWindow.xaml.cs"), "private void Racine_MouseLeftButtonUp");
        Assert.Contains("_geste.Relache()", corps, StringComparison.Ordinal);
        Assert.Contains("ClicCentre(", corps, StringComparison.Ordinal);
        Assert.Contains("ReleaseMouseCapture()", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("e.ClickCount", corps, StringComparison.Ordinal);       // il vaut 0 au relâchement
        Assert.DoesNotContain("ToggleCenterMode()", corps, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_glisser_part_au_seuil_Windows_puis_accroche()
    {
        var corps = Corps(Lire("Views", "MainWindow.xaml.cs"), "private void Racine_MouseMove");
        foreach (var attendu in new[]
                 {
                     "MinimumHorizontalDragDistance", "MinimumVerticalDragDistance", "_geste.Deplacement(", "CommencerGlisser",
                     "ReleaseMouseCapture()", "_enDeplacement = true", "finally", "DragMove();", "SnapToNearestCorner()",
                 })
            Assert.Contains(attendu, corps, StringComparison.Ordinal);
        Assert.True(corps.IndexOf("DragMove();", StringComparison.Ordinal) < corps.IndexOf("SnapToNearestCorner()", StringComparison.Ordinal),
                    "l'accroche doit suivre le RETOUR de DragMove");
    }

    [Fact]
    public void La_perte_de_capture_est_transmise()
    {
        var corps = Corps(Lire("Views", "MainWindow.xaml.cs"), "private void Racine_LostMouseCapture");
        Assert.Contains("_geste.PerteCapture()", corps, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_commentaires_faux_ont_disparu()
    {
        var xaml = Lire("Views", "MainWindow.xaml");
        var code = Lire("Views", "MainWindow.xaml.cs");
        foreach (var faux in new[] { "Transparent suffit", "CentreHit le prouve", "continue d'atteindre CentreHit", "clic au centre" })
        {
            Assert.DoesNotContain(faux, xaml, StringComparison.Ordinal);
            Assert.DoesNotContain(faux, code, StringComparison.Ordinal);
        }
        Assert.Contains("ZoneSilhouette", xaml, StringComparison.Ordinal);
        // Le seul « Transparent » est le Background de la balise <Window> : il DOIT rester alpha 0 (hors silhouette, le clic traverse).
        Assert.Equal(1, Compter(xaml, "\"Transparent\""));
        Assert.DoesNotContain("#01000000", xaml, StringComparison.Ordinal);   // le token, jamais l'alpha littéral
    }

    [Fact]
    public void Le_delai_vient_du_systeme_et_les_automates_restent_purs()
    {
        Assert.Contains("GetDoubleClickTime", Lire("Interop", "NativeMethods.cs"), StringComparison.Ordinal);

        var arbitre = Lire("ViewModels", "ArbitreClicCentre.cs");
        Assert.DoesNotContain("System.Windows", arbitre, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", arbitre, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", arbitre, StringComparison.Ordinal);

        Assert.DoesNotContain("Thread.Sleep", Lire("ViewModels", "MainViewModel.cs"), StringComparison.Ordinal);

        var automate = Lire("ViewModels", "AutomateGeste.cs");
        Assert.DoesNotContain("System.Windows", automate, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", automate, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_restauration_sans_taille_est_recalee_a_la_premiere_mise_en_page()
    {
        // Filet CAD-02 : si RestorePlacement a posé la fenêtre sans taille connue, la première mise en page recale une fois.
        var code = Lire("Views", "MainWindow.xaml.cs");
        Assert.Contains("RestaurationSansTaille", code, StringComparison.Ordinal);
    }

    /// <summary>Chemin de docs/ INJECTÉ par MSBuild (même attribut que <see cref="GardeDocumentationHistoriqueTests"/>), jamais deviné.</summary>
    private static string CheminDocs()
    {
        var racine = typeof(GardeGestesCadranTests).Assembly
                         .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                         .Cast<System.Reflection.AssemblyMetadataAttribute>()
                         .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value ?? "";
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    private static string LireDoc(string chemin)
    {
        Assert.True(File.Exists(chemin), $"Document introuvable : {chemin}");
        var texte = File.ReadAllText(chemin).Replace("\r\n", "\n");
        Assert.True(texte.Length > 200, $"{chemin} est suspicieusement court : la garde ne lit pas le vrai fichier.");
        return texte;
    }

    /// <summary>GST-01 (mots) : le double-clic marche sur toute la silhouette ; dire « au centre » ferait chercher une zone
    /// qui n'existe plus. Le README, la note de la 3.5.0 et la carte Historique des réglages disent le geste unique. Les notes
    /// des versions passées (3.3.0 dans docs/publish.md) sont de l'histoire et ne sont pas lues ici.</summary>
    [Fact]
    public void La_documentation_des_gestes_suit_le_geste_unique()
    {
        var readme = LireDoc(Path.GetFullPath(Path.Combine(CheminDocs(), "..", "README.md")));
        foreach (var perime in new[] { "au centre du cadran", "clic au centre", "Un **clic** au centre" })
            Assert.DoesNotContain(perime, readme, StringComparison.Ordinal);
        Assert.Contains("double-clic sur le cadran", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("glisser", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clic droit", readme, StringComparison.OrdinalIgnoreCase);

        // Section « Premier lancement de la 3.5.0 » : de son titre au titre suivant (ou la fin du fichier).
        var publish = LireDoc(Path.Combine(CheminDocs(), "publish.md"));
        const string titre = "### Premier lancement de la 3.5.0";
        var debut = publish.IndexOf(titre, StringComparison.Ordinal);
        Assert.True(debut >= 0, $"« {titre} » introuvable dans docs/publish.md");
        var suivant = publish.IndexOf("\n#", debut + titre.Length, StringComparison.Ordinal);
        var note = suivant > 0 ? publish[debut..suivant] : publish[debut..];
        Assert.Contains("double-clic", note, StringComparison.Ordinal);
        Assert.Contains("glisser", note, StringComparison.Ordinal);

        var reglages = Lire("Views", "Reglages", "ReglagesWindow.xaml");
        Assert.Contains("Aussi : double-clic sur le cadran", reglages, StringComparison.Ordinal);
        Assert.DoesNotContain("au centre du cadran", reglages, StringComparison.Ordinal);
    }

    /// <summary>CAD-02 : le changement d'empreinte recale sur le coin COURANT (jamais le plus proche), sans persistance,
    /// sans toucher au z-order ni passer par Window.Left/Top ; ignoré pendant un glisser.</summary>
    [Fact]
    public void Le_changement_d_empreinte_recale_sur_le_coin_courant()
    {
        var vue = Lire("Views", "MainWindow.xaml.cs");
        Assert.Contains("SizeChanged +=", vue, StringComparison.Ordinal);
        Assert.Contains("RecalerSurCoinCourant()", vue, StringComparison.Ordinal);
        Assert.Contains("PreviousSize", vue, StringComparison.Ordinal);
        Assert.Contains("_enDeplacement = true", vue, StringComparison.Ordinal);
        Assert.Contains("finally", vue, StringComparison.Ordinal);
        Assert.Contains("DragMove();", vue, StringComparison.Ordinal);

        var ctrl = Lire("Services", "OverlayController.cs").Replace("\r\n", "\n");
        const string signature = "public void RecalerSurCoinCourant()";
        var debut = ctrl.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(debut >= 0, "RecalerSurCoinCourant introuvable dans OverlayController.cs");
        var apres = debut + signature.Length;
        var fin = new[]
        {
            ctrl.IndexOf("\n    public ", apres, StringComparison.Ordinal),
            ctrl.IndexOf("\n    private ", apres, StringComparison.Ordinal),
        }.Where(i => i > 0).DefaultIfEmpty(ctrl.Length).Min();
        var corps = ctrl[debut..fin];

        Assert.Contains("CornerSnap.RecalerSurCoin(", corps, StringComparison.Ordinal);
        Assert.Contains("SWP_NOZORDER", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("NearestCorner", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("ClassifyCorner", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("_settings.Save", corps, StringComparison.Ordinal);
        Assert.DoesNotContain(".Left =", corps, StringComparison.Ordinal);
        Assert.DoesNotContain(".Top =", corps, StringComparison.Ordinal);
    }
}
