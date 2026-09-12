using System.Linq;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// EVT-01 / EVT-02 — la parade au mode de défaillance le plus dangereux du contrat externe : le sort
/// d'un nom d'événement INCONNU n'est pas documenté, donc un nom mal orthographié pourrait laisser un
/// hook MORT et MUET dans la configuration de l'utilisateur.
///
/// <para>Ces tests sont PURS : aucune E/S, aucun accès au profil utilisateur, aucune horloge.</para>
/// </summary>
public class CatalogueEvenementsHooksTests
{
    /// <summary>Les dix événements confirmés SANS support de « matcher » (section F du relevé).</summary>
    private static readonly string[] SansMatcher =
    {
        "UserPromptSubmit", "PostToolBatch", "Stop", "TeammateIdle", "TaskCreated",
        "TaskCompleted", "WorktreeCreate", "WorktreeRemove", "MessageDisplay", "CwdChanged",
    };

    /// <summary>C'est CE test qui fixe le nombre : compter des lignes de source ne compterait pas des entrées.</summary>
    [Fact]
    public void Le_catalogue_compte_trente_trois_noms_distincts()
    {
        Assert.Equal(33, CatalogueEvenementsHooks.Tous.Length);
        Assert.Equal(33, CatalogueEvenementsHooks.Tous.Select(e => e.Nom).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EstConnu_reconnait_les_noms_exacts_du_releve()
    {
        Assert.True(CatalogueEvenementsHooks.EstConnu("PermissionRequest"));
        Assert.True(CatalogueEvenementsHooks.EstConnu("Notification"));
        Assert.True(CatalogueEvenementsHooks.EstConnu("MessageDisplay"));
        Assert.True(CatalogueEvenementsHooks.EstConnu("ElicitationResult"));
    }

    /// <summary>
    /// Une faute de frappe, une casse différente, le vide et des espaces sont TOUS refusés : les clés de
    /// settings.json sont comparées telles quelles, sans rognage implicite.
    /// </summary>
    [Fact]
    public void EstConnu_refuse_une_faute_de_frappe_une_casse_differente_et_tout_rognage()
    {
        Assert.False(CatalogueEvenementsHooks.EstConnu("Notifcation"));   // faute de frappe
        Assert.False(CatalogueEvenementsHooks.EstConnu("notification"));  // casse différente
        Assert.False(CatalogueEvenementsHooks.EstConnu(null));
        Assert.False(CatalogueEvenementsHooks.EstConnu(""));
        Assert.False(CatalogueEvenementsHooks.EstConnu("  Stop  "));      // aucun rognage implicite
    }

    /// <summary>
    /// Un « matcher » posé sur l'un de ces dix événements serait silencieusement ignoré : le groupe
    /// installé ne ferait pas ce qu'il annonce.
    /// </summary>
    [Fact]
    public void Les_dix_evenements_sans_support_de_matcher_sont_refuses()
    {
        Assert.Equal(10, SansMatcher.Length);
        foreach (var nom in SansMatcher)
        {
            Assert.True(CatalogueEvenementsHooks.EstConnu(nom), nom + " doit être au catalogue");
            Assert.False(CatalogueEvenementsHooks.AccepteUnMatcher(nom), nom + " n'accepte PAS de matcher");
        }
    }

    [Fact]
    public void Les_vingt_trois_autres_evenements_acceptent_un_matcher()
    {
        var acceptants = CatalogueEvenementsHooks.Tous
            .Where(e => !SansMatcher.Contains(e.Nom, StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(23, acceptants.Length);
        foreach (var e in acceptants)
            Assert.True(CatalogueEvenementsHooks.AccepteUnMatcher(e.Nom), e.Nom + " accepte un matcher");

        // Les deux que le câblage de Chronos filtre réellement.
        Assert.True(CatalogueEvenementsHooks.AccepteUnMatcher("Notification"));
        Assert.True(CatalogueEvenementsHooks.AccepteUnMatcher("PermissionRequest"));
    }

    /// <summary>Ce qu'on ne connaît pas n'accepte rien.</summary>
    [Fact]
    public void Un_nom_inconnu_n_accepte_aucun_matcher()
    {
        Assert.False(CatalogueEvenementsHooks.AccepteUnMatcher("Inconnu"));
        Assert.False(CatalogueEvenementsHooks.AccepteUnMatcher("Notifcation"));
        Assert.False(CatalogueEvenementsHooks.AccepteUnMatcher(null));
        Assert.False(CatalogueEvenementsHooks.AccepteUnMatcher(""));
    }

    /// <summary>
    /// GARDE ANTI-MUTISME. Si l'un des noms que Chronos câble aujourd'hui manquait au catalogue, la
    /// validation d'installation le rejetterait — et le widget deviendrait muet sans un mot d'erreur.
    /// </summary>
    [Fact]
    public void Les_cinq_noms_cables_aujourd_hui_sont_tous_connus()
    {
        foreach (var nom in new[] { "SessionStart", "SessionEnd", "UserPromptSubmit", "Stop", "Notification" })
            Assert.True(CatalogueEvenementsHooks.EstConnu(nom), nom + " est câblé : il DOIT être au catalogue");
    }
}
