using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Garde des COULEURS des quatre cadrans alternatifs (THM-04, plan 39-03) : Braises, Fusible, Marée et Volets ne portent
/// plus aucune couleur fixe. Leurs vues (<c>Views/Cadrans/*.xaml</c>) lisent les pinceaux du thème en <c>DynamicResource</c>
/// et leurs contrôles (<c>Controls/Cadrans/*.cs</c>) n'ont plus de défaut coloré : c'est la vue qui fournit chaque pinceau.
///
/// Ce que la garde attrape et que <c>dotnet build</c> ne voit pas : un hexadécimal recopié dans une vue (le cadran
/// resterait en minuit sur un thème clair), un <c>Frozen(0x…)</c> réintroduit comme défaut de DP, ou la fusion locale de
/// <c>DesignTokens.xaml</c> dans une vue de cadran, qui masquerait les tokens écrits par <c>MainWindow.ApplyThemeBrushes</c>
/// (piège 34-08). Lecture texte des sources : fait pur, sans STA.
/// </summary>
public class GardeCouleursCadransTests
{
    /// <summary>Hexadécimal, ou attribut de pinceau / couleur écrit en clair (ni balisage <c>{…}</c>, ni <c>Transparent</c>,
    /// seul littéral toléré : le fond de hit-test de la racine).</summary>
    public static readonly Regex InterditXaml = new(
        "#[0-9A-Fa-f]{3,8}\\b|\\b(Foreground|Background|Fill|Stroke|BorderBrush|Brush|Color)=\"(?!\\{|Transparent\")",
        RegexOptions.Compiled);

    /// <summary>Pinceau ou couleur fabriqués à partir de littéraux en C#. Une couleur DÉRIVÉE (<c>WithAlpha</c> :
    /// <c>Color.FromArgb(a, c.R, c.G, c.B)</c>) reste permise ; seul pinceau nommé toléré : <c>Brushes.Transparent</c>.</summary>
    public static readonly Regex InterditCs = new(
        @"Frozen\(0x|FrozenA\(0x|Color\.From(A)?[Rr]gb\(\s*(0x|\d)|Colors\.[A-Z]|Brushes\.(?!Transparent\b)[A-Z]",
        RegexOptions.Compiled);

    /// <summary>Fusion locale d'un dictionnaire dans une vue de cadran : interdite (masquerait les tokens du thème).</summary>
    private static readonly Regex InterditFusion = new("MergedDictionaries|DesignTokens\\.xaml", RegexOptions.Compiled);

    private static string Racine()
    {
        var racine = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine),
            "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        return racine;
    }

    [Fact]
    public void Aucune_couleur_litterale_dans_les_cadrans_alternatifs()
    {
        var racine = Racine();
        var vues = Path.Combine(racine, "Views", "Cadrans");
        var controles = Path.Combine(racine, "Controls", "Cadrans");

        var xamls = Directory.Exists(vues) ? Directory.EnumerateFiles(vues, "*.xaml").ToList() : new List<string>();
        var cs = Directory.Exists(controles) ? Directory.EnumerateFiles(controles, "*.cs").ToList() : new List<string>();

        // Anti-mutisme : une garde qui ne lit rien passerait toujours — cinq vues (Arcs extrait en phase 40), quatre contrôles.
        Assert.True(xamls.Count == 5, $"la garde doit voir exactement 5 vues de cadran (Arcs extrait en phase 40) ({xamls.Count}) : {vues}");
        Assert.True(cs.Count == 4, $"la garde doit voir exactement 4 contrôles de cadran ({cs.Count}) : {controles}");

        var infractions = new List<string>();
        foreach (var (fichiers, motifs) in new[]
                 {
                     (xamls, new[] { InterditXaml, InterditFusion }),
                     (cs, new[] { InterditCs }),
                 })
        {
            foreach (var fichier in fichiers)
            {
                var lignes = File.ReadAllLines(fichier);
                for (var i = 0; i < lignes.Length; i++)
                    if (motifs.Any(m => m.IsMatch(lignes[i])))
                        infractions.Add($"{Path.GetRelativePath(racine, fichier)}:{i + 1}: {lignes[i].Trim()}");
            }
        }

        Assert.True(infractions.Count == 0,
            "THM-04 : une couleur est écrite EN DUR dans un cadran alternatif (ou un dictionnaire y est fusionné) — les "
            + "vues lisent les pinceaux du thème en {DynamicResource …}, les contrôles n'ont aucun défaut coloré.\n  "
            + string.Join("\n  ", infractions));
    }

    /// <summary>Auto-test de la garde : elle attrape les motifs réellement présents avant 39-03 et laisse passer les
    /// formes permises (liaison au thème, fond de hit-test, couleur dérivée).</summary>
    [Theory]
    [InlineData("xaml", "Foreground=\"#F4F2EC\"", true)]
    [InlineData("xaml", "Background=\"#211D2A\"", true)]
    [InlineData("xaml", "<Pen Brush=\"#55000000\" Thickness=\"1\"/>", true)]
    [InlineData("xaml", "Foreground=\"{DynamicResource TextePrincipal}\"", false)]
    [InlineData("xaml", "Background=\"Transparent\"", false)]
    [InlineData("cs", "new FrameworkPropertyMetadata(Frozen(0x46, 0x44, 0x4F), opts)", true)]
    [InlineData("cs", "var quota = QuotaBrush ?? Brushes.Gray;", true)]
    [InlineData("cs", "new SolidColorBrush(Color.FromRgb(0x14, 0x10, 0x19))", true)]
    [InlineData("cs", "new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B))", false)]
    [InlineData("cs", "var vide = Brushes.Transparent;", false)]
    public void La_garde_attrape_les_motifs_interdits(string sorte, string ligne, bool interdit)
    {
        var motif = sorte == "xaml" ? InterditXaml : InterditCs;
        Assert.Equal(interdit, motif.IsMatch(ligne));
    }
}
