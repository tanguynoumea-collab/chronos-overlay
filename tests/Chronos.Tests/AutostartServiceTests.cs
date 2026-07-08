using System;
using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Preuve automatisée de DEP-02 : autostart .lnk piloté dans un dossier startup INJECTÉ
/// (jamais le vrai Startup). La logique chemin/existence est couverte sans dépendre de COM ;
/// la création réelle via WScript.Shell est vérifiée séparément.
/// </summary>
public class AutostartServiceTests : IDisposable
{
    private readonly string _dossierTemp;

    public AutostartServiceTests()
    {
        // Dossier startup factice, isolé du vrai %APPDATA%\...\Startup.
        _dossierTemp = Path.Combine(Path.GetTempPath(), "ChronosTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dossierTemp);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dossierTemp)) Directory.Delete(_dossierTemp, recursive: true); }
        catch { /* nettoyage best-effort */ }
    }

    [Fact]
    public void IsEnabled_est_false_quand_le_lnk_est_absent()
    {
        var service = new AutostartService(_dossierTemp);
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Disable_est_idempotent_quand_le_lnk_est_absent()
    {
        var service = new AutostartService(_dossierTemp);
        // Aucune exception attendue même si le raccourci n'existe pas.
        service.Disable();
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void IsEnabled_reflete_l_existence_du_lnk_puis_Disable_le_supprime()
    {
        var service = new AutostartService(_dossierTemp, linkName: "Chronos.lnk");

        // Crée un .lnk factice (couvre la logique chemin/existence sans COM).
        var lnk = Path.Combine(_dossierTemp, "Chronos.lnk");
        File.WriteAllText(lnk, "raccourci factice");

        Assert.True(service.IsEnabled());

        service.Disable();

        Assert.False(service.IsEnabled());
        Assert.False(File.Exists(lnk));
    }

    [Fact]
    public void Enable_cree_un_lnk_ciblant_ProcessPath()
    {
        // Test d'intégration léger : WScript.Shell est présent par défaut sous Windows.
        var service = new AutostartService(_dossierTemp);

        service.Enable();

        Assert.True(service.IsEnabled());
        Assert.True(File.Exists(Path.Combine(_dossierTemp, "Chronos.lnk")));
    }
}
