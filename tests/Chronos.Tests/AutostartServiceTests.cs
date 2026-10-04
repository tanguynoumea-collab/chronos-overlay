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

    /// <summary>PKG-R1 : version lue dans le NOM de l'exe factice (« Chronos-v3.4.0.exe » → 3.4.0) ; les fichiers factices sont
    /// vides, sans ressource de version.</summary>
    private static Version? VersionParNom(string chemin)
    {
        var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(chemin), @"-v(\d+\.\d+\.\d+)\.exe$");
        return m.Success ? Version.Parse(m.Groups[1].Value) : null;
    }

    /// <summary>Service de test : les exe factices vivent sous %TEMP% ; on désigne un AUTRE dossier comme « temporaire »
    /// pour exercer la règle des versions (la règle %TEMP% a ses propres tests).</summary>
    private AutostartService Service(string startup, string exe)
        => new(startup, exePath: exe, lireVersion: VersionParNom,
               dossierTemporaire: Path.Combine(_dossierTemp, "un-autre-temp"));

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
        var service = Service(startup, courant);

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
        var service = Service(startup, courant);

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

    [Fact]
    public void Garde_le_demarrage_converge_l_autostart_apres_le_Host_et_avant_le_MainViewModel_dans_un_try()
    {
        var racine = CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var app = File.ReadAllText(Path.Combine(racine, "App.xaml.cs"));

        var build = app.IndexOf("_host = builder.Build();", StringComparison.Ordinal);
        var appel = app.IndexOf("ConvergerVersExeCourant()", StringComparison.Ordinal);
        var vm = app.IndexOf("GetRequiredService<MainViewModel>()", StringComparison.Ordinal);

        Assert.True(build >= 0, "« _host = builder.Build(); » introuvable dans App.xaml.cs");
        Assert.True(appel >= 0, "App.xaml.cs n'appelle pas ConvergerVersExeCourant() (PKG-1)");
        Assert.True(vm >= 0, "« GetRequiredService<MainViewModel>() » introuvable dans App.xaml.cs");
        Assert.True(build < appel, "la convergence de l'autostart doit suivre la construction du Host");
        Assert.True(appel < vm, "la convergence de l'autostart doit précéder la résolution du MainViewModel (la case lit IsEnabled)");
        Assert.Equal(appel, app.LastIndexOf("ConvergerVersExeCourant()", StringComparison.Ordinal));   // un seul appel
        Assert.Contains("catch", app.Substring(appel, vm - appel), StringComparison.Ordinal);       // protégé : jamais bloquant
    }

    private static string CheminSources()
        => typeof(AutostartServiceTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";

    // ------------------------------------------------------------------------------------------
    // 42.2-11 (PKG-R1) : « dernier lancé gagne » abandonné — on ne repointe que vers PLUS RÉCENT ou vers une cible disparue
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Une_cible_plus_recente_est_conservee_Ignore()
    {
        var (ancien, courant) = ExesFactices();
        var startup = DossierStartup();
        new AutostartService(startup, exePath: courant).Enable();   // la 3.5.0 tient l'autostart
        var service = Service(startup, ancien);                      // on relance une 3.4.0 restée sur le disque

        Assert.Equal(BilanAutostart.Ignore, service.ConvergerVersExeCourant());
        Assert.True(MemeChemin(service.CibleDuRaccourci(), courant));
        Assert.False(service.IsEnabled());                           // inchangé : la cible n'est pas l'exe courant
    }

    [Fact]
    public void Une_cible_de_meme_version_est_conservee_Ignore()
    {
        var startup = DossierStartup();
        var copie = Path.Combine(_dossierTemp, "Telechargements", "Chronos-v3.5.0.exe");
        var installe = Path.Combine(_dossierTemp, "Outils", "Chronos-v3.5.0.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(copie)!);
        Directory.CreateDirectory(Path.GetDirectoryName(installe)!);
        File.WriteAllBytes(copie, Array.Empty<byte>());
        File.WriteAllBytes(installe, Array.Empty<byte>());
        new AutostartService(startup, exePath: installe).Enable();

        Assert.Equal(BilanAutostart.Ignore, Service(startup, copie).ConvergerVersExeCourant());
        Assert.True(MemeChemin(Service(startup, copie).CibleDuRaccourci(), installe));
    }

    [Fact]
    public void Une_cible_absente_du_disque_est_repointee()
    {
        var (ancien, courant) = ExesFactices();
        var startup = DossierStartup();
        new AutostartService(startup, exePath: courant).Enable();
        File.Delete(courant);                                        // l'exe visé a été supprimé
        var service = Service(startup, ancien);

        Assert.Equal(BilanAutostart.Repointe, service.ConvergerVersExeCourant());
        Assert.True(MemeChemin(service.CibleDuRaccourci(), ancien));
    }

    [Fact]
    public void Des_versions_illisibles_conservent_le_raccourci()
    {
        var (ancien, courant) = ExesFactices();
        var startup = DossierStartup();
        new AutostartService(startup, exePath: ancien).Enable();
        var service = new AutostartService(startup, exePath: courant, lireVersion: _ => null,
                                           dossierTemporaire: Path.Combine(_dossierTemp, "un-autre-temp"));

        Assert.Equal(BilanAutostart.Ignore, service.ConvergerVersExeCourant());
        Assert.True(MemeChemin(service.CibleDuRaccourci(), ancien));
    }

    [Fact]
    public void Un_exe_sous_bin_est_un_build_de_developpement_Ignore()
    {
        var (ancien, _) = ExesFactices();
        var startup = DossierStartup();
        new AutostartService(startup, exePath: ancien).Enable();
        var build = Path.Combine(_dossierTemp, "src", "Chronos", "bin", "Debug", "net8.0-windows", "Chronos-v9.9.9.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(build)!);
        File.WriteAllBytes(build, Array.Empty<byte>());

        Assert.Equal(BilanAutostart.Ignore, Service(startup, build).ConvergerVersExeCourant());
        Assert.True(MemeChemin(Service(startup, build).CibleDuRaccourci(), ancien));
    }

    [Fact]
    public void Un_exe_sous_le_dossier_temporaire_Ignore()
    {
        var (ancien, courant) = ExesFactices();
        var startup = DossierStartup();
        new AutostartService(startup, exePath: ancien).Enable();
        // Ici le dossier temporaire est le VRAI emplacement des exe factices.
        var service = new AutostartService(startup, exePath: courant, lireVersion: VersionParNom, dossierTemporaire: _dossierTemp);

        Assert.Equal(BilanAutostart.Ignore, service.ConvergerVersExeCourant());
        Assert.True(MemeChemin(service.CibleDuRaccourci(), ancien));
    }

    [Theory]
    [InlineData(@"C:\Dev\Chronos\src\Chronos\bin\Release\net8.0-windows\win-x64\Chronos.exe", true)]
    [InlineData(@"C:\Dev\Chronos\BIN\Chronos.exe", true)]
    [InlineData(@"C:\Temp\Chronos.exe", true)]
    [InlineData(@"C:\Temp\sous\Chronos.exe", true)]
    [InlineData(@"C:\Outils\Chronos-v3.5.0.exe", false)]
    [InlineData(@"C:\Outils\binaires\Chronos.exe", false)]
    [InlineData(@"C:\TempX\Chronos.exe", false)]
    public void Emplacement_jetable(string exe, bool attendu)
        => Assert.Equal(attendu, AutostartService.EmplacementJetable(exe, @"C:\Temp\"));

    [Fact]
    public void Garde_le_demarrage_journalise_un_raccourci_conserve()
    {
        var app = File.ReadAllText(Path.Combine(CheminSources(), "App.xaml.cs"));
        Assert.Contains("BilanAutostart.Ignore", app, StringComparison.Ordinal);
        Assert.Contains("raccourci conservé", app, StringComparison.Ordinal);
    }
}
