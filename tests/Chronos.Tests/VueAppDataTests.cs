using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CPT-02 — le diagnostic doit dire depuis quelle VUE d'AppData il lit. Tout processus lancé SOUS l'app bureau
/// Claude (session, hook, dotnet test, agent) voit la copie copy-on-write du paquet MSIX ; l'overlay lancé par
/// l'Explorateur voit la vue réelle. Dans la vue réelle, %APPDATA%\Claude est ABSENT : sa présence signe la vue
/// virtualisée (docs/desktop-app-sessions.md §7, sondes 29/31, recherche 32). C'est ce malentendu qui a fait
/// croire à un « gel » de last-exact.json. Détection PURE sur un chemin injecté — jamais le vrai %APPDATA% ici.
/// </summary>
public class VueAppDataTests : IDisposable
{
    private readonly string _dir;

    public VueAppDataTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosVueAppData_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    [Fact]
    public void Un_dossier_Claude_present_signe_la_vue_virtualisee()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Claude"));

        Assert.Equal(VueAppData.Virtualisee, DetecteurVueAppData.Detecter(_dir));
    }

    [Fact]
    public void Sans_dossier_Claude_la_vue_est_reelle()
        => Assert.Equal(VueAppData.Reelle, DetecteurVueAppData.Detecter(_dir));

    [Fact]
    public void Le_libelle_nomme_la_vue_et_avertit_seulement_si_virtualisee()
    {
        var reelle = DetecteurVueAppData.Libelle(VueAppData.Reelle);
        var virtualisee = DetecteurVueAppData.Libelle(VueAppData.Virtualisee);

        Assert.Equal("réelle", reelle);
        Assert.StartsWith("virtualisée (paquet de l'app bureau)", virtualisee);
        Assert.Contains("ne sont pas ceux de l'overlay", virtualisee);
        Assert.DoesNotContain("ne sont pas ceux de l'overlay", reelle);   // l'avertissement n'est pas un bruit de fond
    }
}
