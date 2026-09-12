using System.IO;
using System.Reflection;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDE DE NON-RETOUR de la phase 21 (SRC-01). Le widget de sessions ne parle que de Claude Code : la
/// source app-bureau par UI Automation, son poll de fond et l'observateur de focus OS ont été retirés.
///
/// La garde est par RÉFLEXION sur l'assembly, comme le précédent
/// <c>Aucun_type_de_plafond_ne_subsiste_dans_l_assembly</c> (phase 16) : elle attrape un rétablissement
/// depuis l'historique, une réimplémentation sous un autre fichier, ou un copier-coller — trois voies
/// qu'une recherche textuelle sur un chemin de fichier manquerait.
///
/// Pourquoi cette suppression n'est PAS du ménage opportuniste : l'acquittement par focus exigeait une
/// session d'origine « app bureau » ET un identifiant synthétique de cette source. Une session Claude Code
/// a une origine ligne de commande et un UUID — le mécanisme ne l'atteignait jamais, et le sondage de
/// focus OS était payé à chaque tick pour un résultat jamais lu.
///
/// Ces tests ne lisent que l'assembly et des fichiers du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.
/// </summary>
public class GardesPerimetreTests
{
    [Fact]
    public void Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly()
    {
        var asm = typeof(Chronos.Services.IUsageProvider).Assembly;

        var revenants = asm.GetTypes()
            .Where(t => t.Name.Contains("Uia", StringComparison.Ordinal)
                     || t.Name.EndsWith("ForegroundWatch", StringComparison.Ordinal)
                     || t.Name == "DesktopHealth"
                     || t.Name == "SessionKind"
                     || t.Name == "SessionOrigin")
            .Select(t => t.FullName)
            .ToList();

        Assert.True(revenants.Count == 0,
            "La source app-bureau par UI Automation a été retirée en phase 21 (SRC-01) : le widget ne "
            + "montre que des sessions Claude Code. Ces types ne doivent pas revenir.\n  "
            + string.Join("\n  ", revenants!));
    }

    // Une garde qui ne verrait AUCUN type serait muette (assembly mal résolu, réflexion cassée).
    [Fact]
    public void La_garde_voit_bien_l_assembly_Chronos()
    {
        var asm = typeof(Chronos.Services.IUsageProvider).Assembly;
        Assert.True(asm.GetTypes().Length >= 50, "réflexion muette : l'assembly Chronos n'est pas résolu");
        Assert.Contains(asm.GetTypes(), t => t.Name == "SessionMonitor");
    }

    /// <summary>
    /// Le libellé de type de session (Chat / Code / Cowork) était bindé dans le SEUL template Pastilles —
    /// mesuré : 3 occurrences, toutes entre les lignes 70 et 79 de SessionStyles.xaml, contrairement à ce
    /// qu'annonçait le contexte de phase. Son producteur a disparu avec la source app-bureau : un binding
    /// survivant afficherait une case vide ou décalerait la rangée. La réflexion ne voit pas un binding XAML,
    /// d'où ce contrôle de SOURCE.
    /// </summary>
    [Fact]
    public void Aucun_style_de_session_ne_binde_plus_un_libelle_de_type()
    {
        var fichier = Path.Combine(CheminSources(), "Resources", "SessionStyles.xaml");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette : on exige les 8 templates.
        var templates = System.Text.RegularExpressions.Regex.Matches(texte, "DataTemplate x:Key=").Count;
        Assert.Equal(8, templates);

        Assert.DoesNotContain("KindLabel", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// GARDE DE CÂBLAGE (SRC-02). <c>PurgerPrefixe</c> peut être parfaitement testée et n'être jamais
    /// appelée : le défaut ne se verrait alors que chez l'utilisateur, sur ses propres données, et
    /// silencieusement. Le démarrage en mode overlay doit invoquer la purge du préfixe de l'ancienne
    /// source app-bureau. Contrôle de SOURCE : <c>OnStartup</c> monte un host WPF complet, il n'est pas
    /// instanciable sous test sans lancer l'application — ce que la phase interdit.
    /// </summary>
    [Fact]
    public void Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives()
    {
        var fichier = Path.Combine(CheminSources(), "App.xaml.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");

        var texte = File.ReadAllText(fichier);

        // Une garde qui lirait un fichier vide serait muette.
        Assert.Contains("OnStartup", texte, StringComparison.Ordinal);
        Assert.Contains("PurgerPrefixe(\"desktop:\")", texte, StringComparison.Ordinal);
    }

    /// <summary>Le chemin des sources est INJECTÉ par MSBuild, jamais deviné (Assembly.Location est VIDE
    /// en publication mono-fichier). Motif recopié de <c>GardesDoctrineTests</c>.</summary>
    internal static string CheminSources()
        => typeof(GardesPerimetreTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
