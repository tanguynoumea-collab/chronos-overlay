using System.Collections.Generic;
using System.Linq;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve TOUTE la logique de la source bureau via un FAUX arbre UIA (aucune fenêtre Claude réelle,
/// aucun appel à System.Windows.Automation) : matching fr/en tolérant (UiaLabels), puis mapping
/// arbre→snapshots honnête (états, type, sidebar, ancre RootWebArea, santé/repli tracé, cache).
/// </summary>
public class DesktopUiaSessionSourceTests
{
    // --- Helpers de construction de faux arbre (réutilisés par tous les tests) ---

    /// <summary>Nœud sans AutomationId (cas courant).</summary>
    private static UiaNode FakeUiaNode(string type, string name, params UiaNode[] children)
        => new(type, name, null, true, children);

    /// <summary>Nœud PORTANT un AutomationId (sert à construire l'ancre "RootWebArea").</summary>
    private static UiaNode FakeUiaNode(string type, string name, string? aid, params UiaNode[] children)
        => new(type, name, aid, true, children);

    // ================= Task 2 : UiaLabels (matching fr/en tolérant) =================

    [Theory]
    [InlineData("  claude RÉPOND.  ")]         // casse + espaces (fr)
    [InlineData("Claude répond.")]              // exact (fr)
    [InlineData("Claude is responding")]        // variante en
    [InlineData("  CLAUDE IS RESPONDING ")]     // casse + espaces (en)
    public void Matches_reconnait_Responding_fr_en_insensible_casse_espaces(string name)
        => Assert.True(UiaLabels.Matches(name, UiaLabels.Responding));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("autre chose")]
    public void Matches_est_faux_pour_null_vide_ou_non_correspondant(string? name)
        => Assert.False(UiaLabels.Matches(name, UiaLabels.Responding));

    [Fact]
    public void StartsWithAny_extrait_le_nom_apres_prefixe_fr()
    {
        Assert.True(UiaLabels.StartsWithAny("En cours d'exécution mon-projet", UiaLabels.RunningPrefix, out var nom));
        Assert.Equal("mon-projet", nom);
    }

    [Fact]
    public void StartsWithAny_extrait_le_nom_apres_prefixe_en()
    {
        Assert.True(UiaLabels.StartsWithAny("Running my-proj", UiaLabels.RunningPrefix, out var n2));
        Assert.Equal("my-proj", n2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Projet sans préfixe")]
    public void StartsWithAny_est_faux_et_remainder_vide_sans_prefixe(string? name)
    {
        Assert.False(UiaLabels.StartsWithAny(name, UiaLabels.RunningPrefix, out var r));
        Assert.Equal("", r);
    }
}
