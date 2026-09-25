using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Chronos.Services;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LIB-01 et LIB-03, versant TEXTE : le widget ne parle plus qu'en trois mots — « Réflexion », « En attente »,
/// « En attente ? » — et ces mots n'ont qu'un producteur, <see cref="AffichageSessions"/>. Tout ce qui les
/// RECOPIE (gabarits, galerie, fenêtre, texte d'activation, rôles des hooks, contrat) est lu ici au texte,
/// parce qu'un libellé en dur dans un XAML ou un document n'est vu ni par la réflexion ni par la matrice
/// WPF 8 × 9 (qui mesure des tailles, pas des mots).
///
/// <para>Aucun chargement BAML, aucune fenêtre : lecture de fichiers du dépôt et réflexion seulement. Aucun
/// réseau, aucun <c>%APPDATA%</c>, aucun <c>~/.claude</c>.</para>
/// </summary>
public sealed class LibellesSessionsTests
{
    /// <summary>Les anciens libellés de la v1.6, sous toutes les formes où un fichier pouvait les RECOPIER :
    /// entre guillemets français (prose), entre backticks (tables du contrat), après une flèche (rôles des
    /// hooks), en littéral C# (producteur, échantillons) et le compteur en dur de l'Annonciateur. Le mot nu
    /// « inconnu » reste légitime ailleurs (« un nom inconnu », §5.3) : seules ses formes citées sont visées.</summary>
    private static readonly string[] MotifsInterdits =
    {
        "« à toi »", "« tour fini »", "« en cours »", "à toi ? déduit",
        "`à toi`", "`tour fini`", "`en cours`", "`inconnu`",
        "→ à toi", "→ tour fini", "→ en cours",
        "\"à toi\"", "\"tour fini\"", "\"en cours\"", "\"inconnu\"",
        "Text=\"  en attente\"",
    };

    /// <summary>Le chemin de docs/ est INJECTÉ par MSBuild (motif de <c>ContratHooksDocumenteTests</c>),
    /// jamais deviné depuis l'emplacement d'un assembly.</summary>
    private static string CheminDocs()
        => typeof(LibellesSessionsTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminDocsChronos")?.Value
           ?? "";

    /// <summary>Tout ce qui affiche, recopie ou documente un libellé d'état du widget.</summary>
    private static IReadOnlyList<(string Nom, string Chemin)> FichiersSurveilles()
    {
        var src = GardesPerimetreTests.CheminSources();
        var docs = CheminDocs();
        Assert.False(string.IsNullOrWhiteSpace(src),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        Assert.False(string.IsNullOrWhiteSpace(docs),
            "L'attribut AssemblyMetadata(\"CheminDocsChronos\") manque : cette garde ne lirait pas le contrat.");

        return new (string, string)[]
        {
            ("docs/hooks-contract.md",                Path.Combine(docs, "hooks-contract.md")),
            ("Resources/SessionStyles.xaml",          Path.Combine(src, "Resources", "SessionStyles.xaml")),
            ("Views/SessionsWindow.xaml",             Path.Combine(src, "Views", "SessionsWindow.xaml")),
            ("Views/SessionsGalleryWindow.xaml",      Path.Combine(src, "Views", "SessionsGalleryWindow.xaml")),
            ("Views/SessionsController.cs",           Path.Combine(src, "Views", "SessionsController.cs")),
            ("ViewModels/SessionsViewModel.cs",       Path.Combine(src, "ViewModels", "SessionsViewModel.cs")),
            ("ViewModels/SessionsPreviewViewModel.cs", Path.Combine(src, "ViewModels", "SessionsPreviewViewModel.cs")),
            ("Services/AffichageSessions.cs",         Path.Combine(src, "Services", "AffichageSessions.cs")),
            ("Services/SessionHookInstaller.cs",      Path.Combine(src, "Services", "SessionHookInstaller.cs")),
        };
    }

    /// <summary>Lit un fichier surveillé. ANTI-MUET : un fichier absent ou quasi vide rendrait la garde verte
    /// pour la pire des raisons.</summary>
    private static string Lire(string nom, string chemin)
    {
        Assert.True(File.Exists(chemin), $"{nom} introuvable : {chemin}");
        var texte = File.ReadAllText(chemin);
        Assert.True(texte.Length >= 500, $"{nom} ne fait que {texte.Length} caractères : la garde ne lit pas le vrai fichier.");
        return texte;
    }

    private static string Lire(string relatifAuxSources)
    {
        var chemin = Path.Combine(GardesPerimetreTests.CheminSources(), relatifAuxSources);
        return Lire(relatifAuxSources, chemin);
    }

    [Fact]
    public void Aucun_ancien_libelle_ne_subsiste_a_l_ecran_ni_dans_le_contrat()
    {
        var infractions = new List<string>();
        var fichiers = FichiersSurveilles();
        Assert.Equal(9, fichiers.Count);   // anti-muet : la liste n'a pas été vidée

        foreach (var (nom, chemin) in fichiers)
        {
            var lignes = Lire(nom, chemin).Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lignes.Length; i++)
                foreach (var motif in MotifsInterdits)
                    if (lignes[i].Contains(motif, StringComparison.Ordinal))
                        infractions.Add($"{nom}:{i + 1} — {motif}");
        }

        Assert.True(infractions.Count == 0,
            "LIB-01 : le widget ne parle plus qu'en trois mots (« Réflexion », « En attente », « En attente ? »). "
            + "Un ancien libellé subsiste là où l'utilisateur, la galerie ou le contrat le liraient :\n  "
            + string.Join("\n  ", infractions));

        // ANTI-MUET final : le contrat dit bien les trois mots — une table vidée passerait la recherche d'absence.
        var contrat = Lire(fichiers[0].Nom, fichiers[0].Chemin);
        Assert.Contains("`Réflexion`", contrat, StringComparison.Ordinal);
        Assert.Contains("`En attente`", contrat, StringComparison.Ordinal);
        Assert.Contains("`En attente ?`", contrat, StringComparison.Ordinal);
    }

    /// <summary>Réserve R9 de l'audit v1.6 : le seul texte de l'application qui explique le widget disait
    /// d'autres mots que l'écran. Il est désormais construit par le producteur, à partir de ses trois
    /// constantes ; le contrôleur ne fait plus que l'afficher.</summary>
    [Fact]
    public void Le_texte_d_activation_vient_du_producteur_et_dit_les_trois_mots()
    {
        var texte = AffichageSessions.TexteActivation();

        Assert.Contains("« Réflexion »", texte, StringComparison.Ordinal);
        Assert.Contains("« En attente »", texte, StringComparison.Ordinal);
        Assert.Contains("« En attente ? »", texte, StringComparison.Ordinal);
        foreach (var motif in MotifsInterdits)
            Assert.DoesNotContain(motif, texte, StringComparison.Ordinal);

        var controleur = Lire(Path.Combine("Views", "SessionsController.cs"));
        Assert.Contains("AffichageSessions.TexteActivation()", controleur, StringComparison.Ordinal);
        Assert.DoesNotContain("détectées via", controleur, StringComparison.Ordinal);
    }

    private static int Occurrences(string texte, string motif)
    {
        var n = 0;
        for (var i = texte.IndexOf(motif, StringComparison.Ordinal); i >= 0;
             i = texte.IndexOf(motif, i + motif.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    /// <summary>
    /// Le fantôme (<c>IsGhost</c>) n'a plus de cas vrai depuis que l'état indéterminé n'a plus de ligne
    /// (plan 28-02). Chacun de ses six déclencheurs devient un déclencheur <c>IsDeduced</c> : là où le fantôme
    /// s'effaçait (jusqu'à 0,22), l'attente déduite s'atténue SANS s'effacer — au moins 0,6, jamais 1 (sinon
    /// l'atténuation serait décorative). Un binding vers une propriété disparue ne lève rien, il se tait : d'où
    /// ce contrôle au TEXTE.
    /// </summary>
    [Fact]
    public void Le_fantome_a_quitte_les_gabarits_et_la_deduction_s_attenue_sans_s_effacer()
    {
        var styles = Lire(Path.Combine("Resources", "SessionStyles.xaml"));
        Assert.Equal(8, Regex.Matches(styles, "DataTemplate x:Key=").Count);   // anti-muet
        Assert.DoesNotContain("IsGhost", styles, StringComparison.Ordinal);

        var declencheurs = Regex.Matches(styles,
            "<DataTrigger Binding=\"\\{Binding IsDeduced\\}\" Value=\"True\"><Setter Property=\"Opacity\" Value=\"([0-9.]+)\"/></DataTrigger>");
        Assert.Equal(6, declencheurs.Count);
        foreach (Match m in declencheurs)
        {
            var opacite = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(opacite >= 0.6 && opacite < 1,
                $"Opacité d'atténuation {opacite} : l'attente déduite doit rester lisible (≥ 0,6) et atténuée (< 1).");
        }
        Assert.Equal(6, Occurrences(styles, "IsDeduced"));

        foreach (var relatif in new[]
                 {
                     Path.Combine("Resources", "SessionStyles.xaml"),
                     Path.Combine("Views", "SessionsWindow.xaml"),
                     Path.Combine("Views", "SessionsGalleryWindow.xaml"),
                     Path.Combine("ViewModels", "SessionsViewModel.cs"),
                     Path.Combine("ViewModels", "SessionsPreviewViewModel.cs"),
                 })
        {
            var texte = Lire(relatif);
            Assert.False(texte.Contains("Ghost", StringComparison.Ordinal), $"{relatif} parle encore de Ghost");
            Assert.False(texte.Contains("fantôme", StringComparison.Ordinal), $"{relatif} parle encore de fantôme");
        }
    }

    /// <summary>Six gabarits sur huit ne montrent l'état que par forme et couleur : leur seul canal textuel est
    /// l'info-bulle. Elle dit désormais « projet — mot » sur les huit, et le compteur de l'Annonciateur lit le
    /// producteur.</summary>
    [Fact]
    public void Les_huit_infobulles_disent_le_mot()
    {
        var styles = Lire(Path.Combine("Resources", "SessionStyles.xaml"));
        Assert.Equal(8, Occurrences(styles, "ToolTip=\"{Binding Infobulle}\""));
        Assert.Equal(0, Occurrences(styles, "ToolTip=\"{Binding Project}\""));
        Assert.Equal(1, Occurrences(styles, "Text=\"{Binding LibelleCompteur}\""));
    }

    /// <summary>
    /// PIÈGE 7 : les gabarits sont PARTAGÉS entre l'écran (<c>SessionsViewModel</c>) et la galerie
    /// (<c>SessionsPreviewViewModel</c>). Un binding vers une propriété absente de l'un des deux ne lève
    /// aucune exception et ne rougit pas la matrice 8 × 9 (qui mesure des tailles) : il se TAIT. Chaque nom
    /// lié doit donc exister sur la ligne (<c>SessionItemVm</c>), ou sur les DEUX listes.
    /// </summary>
    [Fact]
    public void Tout_ce_que_les_gabarits_lient_existe_sur_les_deux_ViewModels()
    {
        var styles = Lire(Path.Combine("Resources", "SessionStyles.xaml"));
        var noms = Regex.Matches(styles, "\\{Binding (?:Path=)?([A-Za-z]+)")
                        .Select(m => m.Groups[1].Value)
                        .Where(n => n is not ("PlacementTarget" or "DataContext"))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
        Assert.True(noms.Count >= 10, $"Seulement {noms.Count} noms liés trouvés : la garde ne lit pas les gabarits.");

        static bool Porte(Type t, string nom) => t.GetProperty(nom, BindingFlags.Public | BindingFlags.Instance) is not null;

        var muets = noms.Where(n => !Porte(typeof(SessionItemVm), n)
                                    && !(Porte(typeof(SessionsViewModel), n) && Porte(typeof(SessionsPreviewViewModel), n)))
                        .ToList();
        Assert.True(muets.Count == 0,
            "Bindings MUETS (propriété absente de la ligne, ou de l'un des deux ViewModels de liste) : "
            + string.Join(", ", muets));

        Assert.True(Porte(typeof(SessionsViewModel), "RowOrientation") && Porte(typeof(SessionsPreviewViewModel), "RowOrientation"),
            "RowOrientation (lié via DataContext.RowOrientation) doit exister sur les deux ViewModels de liste.");
        Assert.False(Porte(typeof(SessionItemVm), "IsGhost"), "Le fantôme n'a plus de cas vrai : IsGhost doit disparaître.");

        var galerie = new SessionsPreviewViewModel();
        var mots = new HashSet<string>(galerie.Items.Select(i => i.StateText), StringComparer.Ordinal);
        Assert.True(mots.SetEquals(new[] { AffichageSessions.Reflexion, AffichageSessions.EnAttente, AffichageSessions.EnAttenteDeduite }),
            "Les échantillons de la galerie doivent dire exactement les trois mots : " + string.Join(" | ", mots));
    }
}
