using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// APP-06 — le moniteur du widget lit les fichiers d'état des hooks dans TOUTES les racines d'état : la vue du
/// paquet de l'app bureau ET la vue réelle d'AppData.
///
/// <para><b>Le fait qui a tout déclenché (29-SONDE-HORS-ARBRE.txt, 2026-09-25 21:17).</b> Un hook lancé par l'app
/// bureau (paquet MSIX) écrit « %APPDATA%\Chronos\sessions » dans la vue VIRTUALISÉE du paquet : cinq fichiers
/// sous « %LOCALAPPDATA%\Packages\Claude_…\LocalCache\Roaming\Chronos\sessions ». L'overlay, lancé par explorer,
/// lisait la vue réelle, vide. Le widget en production n'avait jamais vu un fichier de hook d'une session de
/// l'app bureau : il ne vivait que des transcripts.</para>
///
/// <para>Ici, deux racines TEMPORAIRES jouent les deux vues : « P » (le paquet simulé) et « R » (la vue réelle).
/// Une session vue dans les deux est tranchée par l'arbitrage ordinaire (FUS-01, la fraîcheur) — jamais affichée
/// deux fois. Un <see cref="ArchiveStore"/> temporaire est TOUJOURS injecté (sans lui, le moniteur lirait le vrai
/// archived.json) et la source de transcripts est vide : seuls les fichiers de hook parlent.</para>
/// </summary>
public class DeuxVuesAppDataTests : IDisposable
{
    /// <summary>L'instant de référence. Fixe : jamais <c>DateTimeOffset.UtcNow</c>.</summary>
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 25, 20, 0, 0, TimeSpan.Zero);

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string TempRoot(string prefixe)
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-deuxvues-" + prefixe + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _racines.Add(d);
        return d;
    }

    private sealed class SourceVide : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Array.Empty<SessionSnapshot>();
    }

    private ArchiveStore Archive() => new(Path.Combine(TempRoot("archive"), "archived.json"));

    /// <summary>Un fichier d'état au format RÉEL (celui qu'écrit le mode --hook).</summary>
    private static void EcritEtat(string dir, string id, SessionActivity a, DateTimeOffset maj, string? motif = null)
        => File.WriteAllText(Path.Combine(dir, id + ".json"),
               SessionHookProcessor.BuildStateJson(id, "Proj-" + id, a, motif, maj.ToUnixTimeMilliseconds()));

    private SessionMonitor Moniteur(params string[] racines)
        => new(null, new SourceVide(), Archive(), dossiersEtat: racines);

    [Fact]
    public void Le_moniteur_lit_les_fichiers_d_etat_des_deux_racines()
    {
        var p = TempRoot("paquet");
        var r = TempRoot("reel");
        EcritEtat(p, "a", SessionActivity.WaitingAttention, Maintenant.AddMinutes(-2), "PermissionRequest");
        EcritEtat(r, "b", SessionActivity.Working, Maintenant.AddMinutes(-1));

        var lecture = Moniteur(p, r).Inspecter(Maintenant);

        Assert.Equal(2, lecture.Visibles.Count);
        var a = Assert.Single(lecture.Visibles, s => s.SessionId == "a");
        Assert.Equal(SessionActivity.WaitingAttention, a.Activity);
        Assert.Equal("PermissionRequest", a.Reason);
        var b = Assert.Single(lecture.Visibles, s => s.SessionId == "b");
        Assert.Equal(SessionActivity.Working, b.Activity);
    }

    [Fact]
    public void Une_meme_session_vue_dans_deux_racines_est_tranchee_par_la_fraicheur()
    {
        var p = TempRoot("paquet");
        var r = TempRoot("reel");
        EcritEtat(p, "s", SessionActivity.WaitingTurn, Maintenant.AddMinutes(-10));
        EcritEtat(r, "s", SessionActivity.Working, Maintenant.AddMinutes(-1));

        var lecture = Moniteur(p, r).Inspecter(Maintenant);

        var seule = Assert.Single(lecture.Visibles);   // jamais deux lignes pour une session
        Assert.Equal("s", seule.SessionId);
        Assert.Equal(SessionActivity.Working, seule.Activity);
        Assert.Equal(Maintenant.AddMinutes(-1), seule.UpdatedAt);

        var d = Assert.Single(lecture.Desaccords);
        Assert.Equal(SourceSession.Hook, d.SourceRetenue);
        Assert.Equal(SessionActivity.Working, d.EtatRetenu);
        Assert.Equal(SourceSession.Hook, d.SourceEcartee);
        Assert.Equal(SessionActivity.WaitingTurn, d.EtatEcarte);
        Assert.Equal(TimeSpan.FromMinutes(9), d.EcartAge);
    }

    [Fact]
    public void Une_racine_absente_ne_coute_rien_aux_autres()
    {
        var absente = Path.Combine(TempRoot("parent"), "jamais-creee");
        Assert.False(Directory.Exists(absente));
        var r = TempRoot("reel");
        EcritEtat(r, "vivante", SessionActivity.WaitingTurn, Maintenant.AddMinutes(-3));

        var lecture = Moniteur(absente, r).Inspecter(Maintenant);

        Assert.Equal("vivante", Assert.Single(lecture.Visibles).SessionId);
        Assert.Equal(0, lecture.FichiersEcartesParAnciennete);
    }

    [Fact]
    public void Les_fichiers_perimes_sont_comptes_sur_toutes_les_racines()
    {
        var p = TempRoot("paquet");
        var r = TempRoot("reel");
        EcritEtat(p, "vieux-p", SessionActivity.WaitingTurn, Maintenant.AddHours(-9));
        EcritEtat(r, "vieux-r", SessionActivity.WaitingTurn, Maintenant.AddHours(-9));

        var lecture = Moniteur(p, r).Inspecter(Maintenant);

        Assert.Equal(2, lecture.FichiersEcartesParAnciennete);
        Assert.Empty(lecture.Visibles);
    }

    [Fact]
    public void Un_seul_dossier_donne_reste_le_raccourci_des_tests()
    {
        var dir = TempRoot("seul");
        var p = TempRoot("paquet");
        var r = TempRoot("reel");

        var unSeul = new SessionMonitor(dir, new SourceVide(), Archive());
        Assert.Equal(new[] { dir }, unSeul.Dossiers);

        var deux = Moniteur(p, r);
        Assert.Equal(new[] { p, r }, deux.Dossiers);   // même ordre : le paquet d'abord
    }

    /// <summary>Deux façons de dire la même chose sont une ambiguïté : laquelle croire ? Le constructeur refuse
    /// plutôt que de choisir en silence.</summary>
    [Fact]
    public void Deux_facons_de_dire_les_racines_sont_refusees()
    {
        var dir = TempRoot("seul");
        var p = TempRoot("paquet");

        Assert.Throws<ArgumentException>(
            () => new SessionMonitor(dir, new SourceVide(), Archive(), dossiersEtat: new[] { p }));
    }
}
