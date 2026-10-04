using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public void Un_lnk_illisible_ne_compte_pas_comme_active_et_Disable_le_supprime()
    {
        var service = new AutostartService(_dossierTemp, linkName: "Chronos.lnk");

        // .lnk factice (du texte, pas un vrai raccourci) : sa cible est illisible → IsEnabled faux
        // (PKG-1 : « activé » = le raccourci existe ET vise l'exe courant).
        var lnk = Path.Combine(_dossierTemp, "Chronos.lnk");
        File.WriteAllText(lnk, "raccourci factice");

        Assert.False(service.IsEnabled());

        service.Disable();

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

    // ------------------------------------------------------------------------------------------
    // 42.2-08 (PKG-1) : la cible du raccourci est lue, vérifiée et repointée vers l'exe courant
    // ------------------------------------------------------------------------------------------

    /// <summary>Deux exe factices versionnés, dans le dossier temporaire (jamais le vrai dépôt).</summary>
    private (string ancien, string courant) ExesFactices()
    {
        var ancien = Path.Combine(_dossierTemp, "Chronos-v3.4.0.exe");
        var courant = Path.Combine(_dossierTemp, "Chronos-v3.5.0.exe");
        File.WriteAllBytes(ancien, Array.Empty<byte>());
        File.WriteAllBytes(courant, Array.Empty<byte>());
        return (ancien, courant);
    }

    private string DossierStartup() => Path.Combine(_dossierTemp, "Startup");

    private static bool MemeChemin(string? a, string b)
        => a is not null && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Raccourci_absent_Converger_rend_Absent_et_ne_cree_rien()
    {
        var (_, courant) = ExesFactices();
        var startup = DossierStartup();
        Directory.CreateDirectory(startup);
        var service = new AutostartService(startup, exePath: courant);

        Assert.Equal(BilanAutostart.Absent, service.ConvergerVersExeCourant());
        Assert.Empty(Directory.GetFiles(startup));
        Assert.False(service.IsEnabled());
        Assert.Null(service.CibleDuRaccourci());
    }

    [Fact]
    public void Raccourci_vers_l_ancien_exe_est_repointe_vers_l_exe_courant_puis_Conforme_sans_reecriture()
    {
        var (ancien, courant) = ExesFactices();
        var startup = DossierStartup();
        Assert.StartsWith(Path.GetTempPath(), startup, StringComparison.OrdinalIgnoreCase);

        // La 3.4.0 avait créé le raccourci.
        new AutostartService(startup, exePath: ancien).Enable();
        var service = new AutostartService(startup, exePath: courant);

        Assert.False(service.IsEnabled());                         // périmé : la case se lit décochée
        Assert.True(MemeChemin(service.CibleDuRaccourci(), ancien));

        Assert.Equal(BilanAutostart.Repointe, service.ConvergerVersExeCourant());
        Assert.True(MemeChemin(service.CibleDuRaccourci(), courant));
        Assert.True(service.IsEnabled());

        // Second appel : idempotent, le .lnk n'est pas réécrit.
        var lnk = Path.Combine(startup, "Chronos.lnk");
        var avant = File.GetLastWriteTimeUtc(lnk);
        System.Threading.Thread.Sleep(30);
        Assert.Equal(BilanAutostart.Conforme, service.ConvergerVersExeCourant());
        Assert.Equal(avant, File.GetLastWriteTimeUtc(lnk));
    }

    [Fact]
    public void Enable_vise_l_exe_injecte()
    {
        var (_, courant) = ExesFactices();
        var service = new AutostartService(DossierStartup(), exePath: courant);

        service.Enable();

        Assert.True(MemeChemin(service.CibleDuRaccourci(), courant));
        Assert.True(service.IsEnabled());
    }

    [Fact]
    public void Raccourci_corrompu_Converger_ne_leve_jamais()
    {
        var (_, courant) = ExesFactices();
        var startup = DossierStartup();
        Directory.CreateDirectory(startup);
        var octets = new byte[512];
        new Random(42).NextBytes(octets);
        File.WriteAllBytes(Path.Combine(startup, "Chronos.lnk"), octets);
        var service = new AutostartService(startup, exePath: courant);

        var bilan = service.ConvergerVersExeCourant();

        Assert.True(bilan is BilanAutostart.Echec or BilanAutostart.Repointe, $"bilan inattendu : {bilan}");
    }

    [Fact]
    public void Le_membre_par_defaut_de_l_interface_rend_Absent()
    {
        IAutostartService fake = new FakeAutostartService();
        Assert.Equal(BilanAutostart.Absent, fake.ConvergerVersExeCourant());
    }

    [Fact]
    public void Garde_aucun_test_ne_construit_le_service_sur_le_vrai_shell_startup()
    {
        var racine = CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var dossierTests = Path.GetFullPath(Path.Combine(racine, "..", "..", "tests", "Chronos.Tests"));
        Assert.True(Directory.Exists(dossierTests), dossierTests);

        var interdit = "new AutostartService" + "()";   // concaténé : ce fichier ne doit pas se dénoncer lui-même
        var fichiers = Directory.GetFiles(dossierTests, "*.cs", SearchOption.AllDirectories)
                                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                                .ToList();
        Assert.Contains(fichiers, f => f.EndsWith("AutostartServiceTests.cs", StringComparison.Ordinal));   // anti-muet
        foreach (var f in fichiers)
            Assert.DoesNotContain(interdit, File.ReadAllText(f), StringComparison.Ordinal);
    }

    private static string CheminSources()
        => typeof(AutostartServiceTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
