using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// APP-06 — les racines se RÉSOLVENT par candidats, le paquet de l'app bureau d'abord, la vue réelle ensuite.
///
/// <para><b>Le fait (sonde hors de l'arbre de l'app, 29-SONDE-HORS-ARBRE.txt).</b> Un hook lancé par l'app bureau
/// (paquet MSIX) écrit « %APPDATA%\Chronos\sessions » dans la vue VIRTUALISÉE du paquet, physiquement sous
/// « %LOCALAPPDATA%\Packages\Claude_…\LocalCache\Roaming ». L'overlay, lancé par explorer, ne voit pas cette vue.
/// Chaque test construit donc DEUX arborescences temporaires — « Local\Packages\… » et « Roaming » — et les passe à
/// <see cref="RacinesEtat.Candidats"/>. Aucun test n'appelle <see cref="RacinesEtat.ParDefaut"/> : il lirait les
/// dossiers réels de la machine, et ce processus (lancé sous l'app bureau) les voit virtualisés.</para>
/// </summary>
public class RacinesEtatTests : IDisposable
{
    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string TempRoot()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-racines-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _racines.Add(d);
        return d;
    }

    // Une arborescence « <tmp>\Local » (avec les paquets donnés sous Packages) et « <tmp>\Roaming ».
    private (string Racine, string Local, string Roaming) Arborescence(params string[] paquets)
    {
        var racine = TempRoot();
        var local = Path.Combine(racine, "Local");
        var roaming = Path.Combine(racine, "Roaming");
        Directory.CreateDirectory(local);
        Directory.CreateDirectory(roaming);
        foreach (var p in paquets)
            Directory.CreateDirectory(Path.Combine(local, "Packages", p));
        return (racine, local, roaming);
    }

    private static string EtatsDuPaquet(string local, string paquet)
        => Path.Combine(local, "Packages", paquet, "LocalCache", "Roaming", "Chronos", "sessions");

    private static string AppBureauDuPaquet(string local, string paquet)
        => Path.Combine(local, "Packages", paquet, "LocalCache", "Roaming", "Claude", "claude-code-sessions");

    [Fact]
    public void Le_paquet_Claude_passe_avant_la_vue_reelle_pour_les_deux_familles()
    {
        var (_, local, roaming) = Arborescence("Claude_abc123");

        var r = RacinesEtat.Candidats(local, roaming);

        Assert.Equal(new[]
        {
            EtatsDuPaquet(local, "Claude_abc123"),
            Path.Combine(roaming, "Chronos", "sessions"),
        }, r.EtatsHooks);
        Assert.Equal(new[]
        {
            AppBureauDuPaquet(local, "Claude_abc123"),
            Path.Combine(roaming, "Claude", "claude-code-sessions"),
        }, r.SessionsAppBureau);
    }

    [Fact]
    public void Sans_dossier_Packages_seule_la_vue_reelle_reste_et_rien_ne_leve()
    {
        var (_, local, roaming) = Arborescence();   // « Local » existe, « Local\Packages » non
        Assert.False(Directory.Exists(Path.Combine(local, "Packages")));

        var r = RacinesEtat.Candidats(local, roaming);

        Assert.Equal(new[] { Path.Combine(roaming, "Chronos", "sessions") }, r.EtatsHooks);
        Assert.Equal(new[] { Path.Combine(roaming, "Claude", "claude-code-sessions") }, r.SessionsAppBureau);
    }

    [Fact]
    public void Deux_paquets_Claude_sont_candidats_dans_l_ordre_ordinal_et_les_autres_paquets_non()
    {
        var (_, local, roaming) = Arborescence("Claude_zzz", "Claude_aaa", "ClaudeCode_x", "Autre_xyz");

        var r = RacinesEtat.Candidats(local, roaming);

        Assert.Equal(new[]
        {
            EtatsDuPaquet(local, "Claude_aaa"),
            EtatsDuPaquet(local, "Claude_zzz"),
            Path.Combine(roaming, "Chronos", "sessions"),
        }, r.EtatsHooks);
        Assert.Equal(new[]
        {
            AppBureauDuPaquet(local, "Claude_aaa"),
            AppBureauDuPaquet(local, "Claude_zzz"),
            Path.Combine(roaming, "Claude", "claude-code-sessions"),
        }, r.SessionsAppBureau);
        Assert.DoesNotContain(r.EtatsHooks.Concat(r.SessionsAppBureau), c => c.Contains("ClaudeCode_x", StringComparison.Ordinal));
        Assert.DoesNotContain(r.EtatsHooks.Concat(r.SessionsAppBureau), c => c.Contains("Autre_xyz", StringComparison.Ordinal));
    }

    /// <summary>La condition pour que le balayage CYC-01 — qui passe sur les racines d'ÉTAT — ne puisse jamais viser le
    /// dossier de l'app bureau (APP-05) : aucune racine d'état n'y mène. Le contrôle porte sur la partie du chemin
    /// SOUS la racine temporaire, seule partie que <see cref="RacinesEtat"/> fabrique.</summary>
    [Fact]
    public void Les_racines_d_etat_designent_toujours_Chronos_sessions_jamais_le_dossier_de_l_app()
    {
        var (racine, local, roaming) = Arborescence("Claude_zzz", "Claude_aaa", "ClaudeCode_x", "Autre_xyz");

        var r = RacinesEtat.Candidats(local, roaming);

        Assert.Equal(3, r.EtatsHooks.Count);   // garde muette sinon
        var sep = Path.DirectorySeparatorChar.ToString();
        foreach (var e in r.EtatsHooks)
        {
            Assert.EndsWith(Path.Combine("Chronos", "sessions"), e, StringComparison.Ordinal);
            var fabrique = sep + Path.GetRelativePath(racine, e) + sep;
            Assert.DoesNotContain("claude-code-sessions", fabrique, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sep + "Claude" + sep, fabrique, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PremiereExistante_rend_le_premier_candidat_qui_existe_ou_null()
    {
        var racine = TempRoot();
        var absent = Path.Combine(racine, "absent");
        var b = Path.Combine(racine, "B");
        var c = Path.Combine(racine, "C");
        Directory.CreateDirectory(b);
        Directory.CreateDirectory(c);

        Assert.Equal(b, RacinesEtat.PremiereExistante(new[] { absent, b, c }));
        Assert.Null(RacinesEtat.PremiereExistante(new[] { absent, Path.Combine(racine, "absent2") }));
        Assert.Null(RacinesEtat.PremiereExistante(Array.Empty<string>()));
    }

    [Fact]
    public void Aucun_chemin_ne_melange_les_separateurs()
    {
        var (_, local, roaming) = Arborescence("Claude_abc123", "Claude_def456");

        var r = RacinesEtat.Candidats(local, roaming);

        var tous = r.EtatsHooks.Concat(r.SessionsAppBureau).ToList();
        Assert.Equal(6, tous.Count);   // garde muette sinon
        Assert.All(tous, c => Assert.DoesNotContain('/', c));
    }
}
