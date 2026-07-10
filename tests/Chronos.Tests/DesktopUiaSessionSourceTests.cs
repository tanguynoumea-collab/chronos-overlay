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

    /// <summary>Enveloppe des enfants sous l'ancre RootWebArea (ControlType "Document"), dans une fenêtre Claude.</summary>
    private static UiaNode Fenetre(params UiaNode[] contenu)
        => FakeUiaNode("Window", "Claude",
            FakeUiaNode("Document", "", "RootWebArea", contenu));

    private static readonly System.DateTimeOffset Now = new(2026, 07, 10, 12, 0, 0, System.TimeSpan.Zero);

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

    // ================= Task 3 : MapTree (santé / ancre) =================

    [Fact]
    public void MapTree_racine_null_donne_liste_vide_sans_exception()
        => Assert.Empty(DesktopUiaSessionSource.MapTree(null, Now));

    [Fact]
    public void MapTree_fenetre_sans_ancre_RootWebArea_donne_liste_vide()
    {
        // Fenêtre Claude présente mais AUCUN descendant AutomationId=="RootWebArea".
        var arbre = FakeUiaNode("Window", "Claude",
            FakeUiaNode("Pane", "contenu",
                FakeUiaNode("Text", "Claude répond.")));
        Assert.Empty(DesktopUiaSessionSource.MapTree(arbre, Now));
    }

    // ================= Task 3 : états foreground =================

    [Fact]
    public void MapTree_texte_Claude_repond_donne_foreground_Working()
    {
        var arbre = Fenetre(FakeUiaNode("Text", "Claude répond."));
        var snaps = DesktopUiaSessionSource.MapTree(arbre, Now);
        var fg = Assert.Single(snaps);
        Assert.Equal(SessionActivity.Working, fg.Activity);
        Assert.StartsWith("desktop:foreground:", fg.SessionId);
        Assert.Equal(SessionOrigin.Desktop, fg.Origin);
        Assert.Equal(Now, fg.UpdatedAt);
    }

    [Fact]
    public void MapTree_bouton_Arreter_sans_texte_donne_Working()
    {
        var arbre = Fenetre(FakeUiaNode("Button", "Arrêter"));
        var fg = Assert.Single(DesktopUiaSessionSource.MapTree(arbre, Now));
        Assert.Equal(SessionActivity.Working, fg.Activity);
    }

    [Fact]
    public void MapTree_bouton_permission_donne_WaitingAttention()
    {
        var arbre = Fenetre(FakeUiaNode("Button", "Ignorer les permissions"));
        var fg = Assert.Single(DesktopUiaSessionSource.MapTree(arbre, Now));
        Assert.Equal(SessionActivity.WaitingAttention, fg.Activity);
    }

    [Fact]
    public void MapTree_mode_chat_au_repos_donne_WaitingTurn_Chat()
    {
        var arbre = Fenetre(
            FakeUiaNode("Text", "Mode chat"),
            FakeUiaNode("Edit", "Tapez / pour les commandes"));
        var fg = Assert.Single(DesktopUiaSessionSource.MapTree(arbre, Now));
        Assert.Equal(SessionActivity.WaitingTurn, fg.Activity);
        Assert.Equal(SessionKind.Chat, fg.Kind);
        Assert.Equal("desktop:foreground:chat", fg.SessionId);
    }

    [Fact]
    public void MapTree_ancre_sans_signal_donne_Unknown()
    {
        var arbre = Fenetre(FakeUiaNode("Text", "quelque chose d'inconnu"));
        var fg = Assert.Single(DesktopUiaSessionSource.MapTree(arbre, Now));
        Assert.Equal(SessionActivity.Unknown, fg.Activity);
        Assert.Equal(SessionKind.Unknown, fg.Kind);
    }

    // ================= Task 3 : type de session =================

    [Fact]
    public void MapTree_panneaux_Code_donnent_Kind_Code()
    {
        var arbre = Fenetre(
            FakeUiaNode("Text", "Claude répond."),
            FakeUiaNode("Pane", "Terminal"),
            FakeUiaNode("Pane", "Diff"),
            FakeUiaNode("Button", "Actions de session"));
        var fg = Assert.Single(DesktopUiaSessionSource.MapTree(arbre, Now));
        Assert.Equal(SessionKind.Code, fg.Kind);
        Assert.Equal("desktop:foreground:code", fg.SessionId);
    }

    [Fact]
    public void MapTree_controle_a_distance_donne_Cowork_indetermine()
    {
        // BUR-05 : même si un signal d'exécution existe, une session Cowork VM reste Activity=Unknown.
        var arbre = Fenetre(
            FakeUiaNode("Text", "Claude répond."),   // signal d'exécution présent…
            FakeUiaNode("Button", "Contrôle à distance"));
        var fg = Assert.Single(DesktopUiaSessionSource.MapTree(arbre, Now));
        Assert.Equal(SessionKind.Cowork, fg.Kind);
        Assert.Equal(SessionActivity.Unknown, fg.Activity); // …mais l'état distant n'est jamais certain
        Assert.Equal("desktop:foreground:cowork", fg.SessionId);
    }

    // ================= Task 3 : sidebar (BUR-04) =================

    [Fact]
    public void MapTree_sidebar_enumere_les_sessions_en_cours_et_ignore_les_autres()
    {
        var arbre = Fenetre(
            FakeUiaNode("Text", "Mode chat"),
            FakeUiaNode("Edit", "Tapez / pour les commandes"),
            FakeUiaNode("Button", "En cours d'exécution alpha"),
            FakeUiaNode("Button", "En cours d'exécution beta"),
            FakeUiaNode("Button", "projet-inactif")); // PAS de préfixe → ignoré
        var snaps = DesktopUiaSessionSource.MapTree(arbre, Now);

        var alpha = snaps.SingleOrDefault(s => s.Project == "alpha");
        var beta = snaps.SingleOrDefault(s => s.Project == "beta");
        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.Equal(SessionActivity.Working, alpha!.Activity);
        Assert.Equal(SessionActivity.Working, beta!.Activity);
        Assert.Contains(":alpha", alpha.SessionId);
        Assert.Contains(":beta", beta.SessionId);
        Assert.DoesNotContain(snaps, s => s.Project == "projet-inactif");
    }

    // ================= Task 3 : santé / repli tracé (Poll) =================

    private sealed class FakeTreeProvider : IUiaTreeProvider
    {
        private readonly UiaNode? _tree;
        public int Appels { get; private set; }
        public FakeTreeProvider(UiaNode? tree) => _tree = tree;
        public UiaNode? TryGetTree() { Appels++; return _tree; }
    }

    [Fact]
    public void Poll_racine_null_donne_Health_WindowMissing_et_cache_vide()
    {
        var src = new DesktopUiaSessionSource(new FakeTreeProvider(null));
        src.Poll(Now);
        Assert.Equal(DesktopHealth.WindowMissing, src.Health);
        Assert.Empty(src.Read(Now));
    }

    [Fact]
    public void Poll_fenetre_sans_ancre_donne_Health_AnchorMissing_et_cache_vide()
    {
        // Fenêtre Claude présente, ancre RootWebArea absente → repli TRACÉ (pas un zéro silencieux).
        var arbre = FakeUiaNode("Window", "Claude",
            FakeUiaNode("Pane", "contenu"));
        var src = new DesktopUiaSessionSource(new FakeTreeProvider(arbre));
        src.Poll(Now);
        Assert.Equal(DesktopHealth.AnchorMissing, src.Health);
        Assert.Empty(src.Read(Now));
    }

    [Fact]
    public void Poll_avec_ancre_donne_Health_Ok()
    {
        var arbre = Fenetre(FakeUiaNode("Text", "Claude répond."));
        var src = new DesktopUiaSessionSource(new FakeTreeProvider(arbre));
        src.Poll(Now);
        Assert.Equal(DesktopHealth.Ok, src.Health);
        Assert.NotEmpty(src.Read(Now));
    }

    // ================= Task 3 : cache non bloquant (ROB-07) =================

    [Fact]
    public void Read_avant_Poll_est_vide_et_ne_touche_pas_le_provider()
    {
        var provider = new FakeTreeProvider(Fenetre(FakeUiaNode("Text", "Claude répond.")));
        var src = new DesktopUiaSessionSource(provider);
        Assert.Empty(src.Read(Now));
        Assert.Equal(0, provider.Appels); // Read ne fait AUCUNE I/O UIA
    }

    [Fact]
    public void Read_rend_le_cache_sans_rappeler_le_provider()
    {
        var provider = new FakeTreeProvider(Fenetre(FakeUiaNode("Text", "Claude répond.")));
        var src = new DesktopUiaSessionSource(provider);
        src.Poll(Now);
        var appelsApresPoll = provider.Appels;

        var r1 = src.Read(Now);
        var r2 = src.Read(Now);
        Assert.NotEmpty(r1);
        Assert.Same(r1, r2); // même cache
        Assert.Equal(appelsApresPoll, provider.Appels); // aucun rappel entre 2 Read
    }
}
