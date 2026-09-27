using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// GARDES de la section « Journal d'historique » du diagnostic (ACC-03, phase 35, D-35-10 … D-35-12). Le rapport dit la journée
/// par la MÊME lecture que la fenêtre (<c>SourceHistoriqueDisque.LireJour</c>) et par les MÊMES mots (<c>LigneFraicheurJour</c>) :
/// un second chemin — le journal relu et analysé ici même — décrirait une autre journée que celle que l'utilisateur voit, la
/// classe d'erreur que la phase 22 a fermée pour les sessions. La table des processus est relevée UNE fois par rapport (la liste
/// détaillée sous [Magasins persistants] et le compte de la nouvelle section viennent du même relevé), et le fuseau est INJECTÉ :
/// jamais <c>TimeZoneInfo.Local</c> dans ce fichier.
///
/// <para>Fichier NOUVEAU à dessein : <c>GardesPerimetreTests.cs</c> peut être touché par un plan voisin de la même vague.
/// Ces tests ne lisent que des fichiers .cs du dépôt : aucun réseau, aucun %APPDATA%, aucun jeton.</para>
/// </summary>
public class GardeDiagnosticHistoriqueTests
{
    private static string TexteDuDiagnostic()
    {
        var fichier = Path.Combine(GardesPerimetreTests.CheminSources(), "Services", "DiagnosticService.cs");
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
        var texte = File.ReadAllText(fichier);
        // Anti-muet : une garde qui lirait un fichier vide, ou un fichier sans la section, ne prouverait rien.
        Assert.Contains("BuildReportAsync", texte, StringComparison.Ordinal);
        Assert.Contains("[Journal d'historique]", texte, StringComparison.Ordinal);
        return texte;
    }

    private static int Occurrences(string texte, string motif) => Regex.Matches(texte, Regex.Escape(motif)).Count;

    [Fact]
    public void La_journee_se_lit_par_la_facade_de_la_fenetre()
    {
        var texte = TexteDuDiagnostic();

        Assert.Contains(".LireJour(", texte, StringComparison.Ordinal);
        Assert.Contains("LigneFraicheurJour(", texte, StringComparison.Ordinal);
        Assert.DoesNotContain(".Lire(", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyseReleves.Analyser(", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("LecteurJournal.", texte, StringComparison.Ordinal);
    }

    [Fact]
    public void La_table_des_processus_est_relevee_une_seule_fois()
    {
        var texte = TexteDuDiagnostic();

        Assert.Equal(1, Occurrences(texte, "InventaireProcessus.Relever()"));
    }

    [Fact]
    public void Aucun_fuseau_local_dans_le_diagnostic()
    {
        var texte = TexteDuDiagnostic();

        Assert.Contains("TimeZoneInfo? fuseau = null", texte, StringComparison.Ordinal);   // le fuseau est bien un paramètre injecté
        Assert.DoesNotContain("TimeZoneInfo.Local", texte, StringComparison.Ordinal);
    }
}
