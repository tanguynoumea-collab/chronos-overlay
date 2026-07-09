using System.IO;
using Chronos.Placement;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la persistance atomique/tolérante de settings.json (FEN-07) : round-trip, défauts sur
/// fichier absent, défauts SANS exception sur JSON corrompu, écriture atomique (pas de .tmp
/// résiduel) et création du dossier. Chaque test isole un répertoire temp injecté via
/// <c>new ChronosPaths(usage, projects)</c> (ctor positionnel inchangé) → aucun accès au vrai profil.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly ChronosPaths _paths;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "chronos-settings-tests", Path.GetRandomFileName());
        // usage.json dans un sous-dossier « Chronos » simulé ; SettingsFile en dérive.
        var usage = Path.Combine(_dir, "Chronos", "usage.json");
        var projects = Path.Combine(_dir, "projects");
        _paths = new ChronosPaths(usage, projects);
        _service = new SettingsService(_paths);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* nettoyage best-effort */ }
    }

    [Fact]
    public void Save_puis_Load_round_trip()
    {
        var original = new ChronosSettings
        {
            Corner = OverlayCorner.BottomLeft,
            MonitorDeviceName = @"\\.\DISPLAY2",
            X = 123.5,
            Y = 456.75,
            Background = true,
            RefreshIntervalSeconds = 30,
            WeeklyAnchor = new DateTimeOffset(2026, 07, 06, 10, 00, 00, TimeSpan.Zero),
            FiveHourTokenBudget = 88_000,
            WeeklyTokenBudget = 1_200_000,
            OAuthUsageEnabled = false, // valeur ≠ défaut pour prouver la persistance du flag (INT-03)
        };

        _service.Save(original);
        var relu = _service.Load();

        Assert.Equal(original, relu); // égalité de valeur du record
    }

    [Fact]
    public void Load_fichier_absent_redonne_les_defauts()
    {
        var s = _service.Load();

        Assert.Equal(OverlayCorner.TopRight, s.Corner);
        Assert.Equal(60, s.RefreshIntervalSeconds);
        Assert.False(s.Background);
        Assert.Null(s.MonitorDeviceName);
        Assert.Null(s.WeeklyAnchor);
        Assert.Null(s.FiveHourTokenBudget);
        Assert.Null(s.WeeklyTokenBudget);
        Assert.True(s.OAuthUsageEnabled); // défaut true : source exacte active dès l'install (INT-03)
    }

    [Fact]
    public void Load_json_corrompu_redonne_les_defauts_sans_exception()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsFile)!);
        File.WriteAllText(_paths.SettingsFile, "{ ceci n'est pas du JSON valide ]");

        var s = _service.Load(); // ne doit PAS lever

        Assert.Equal(new ChronosSettings(), s);
    }

    [Fact]
    public void Save_est_atomique_aucun_tmp_residuel()
    {
        _service.Save(new ChronosSettings { Corner = OverlayCorner.TopLeft });

        var dir = Path.GetDirectoryName(_paths.SettingsFile)!;
        var residus = Directory.GetFiles(dir, "*.tmp-*");
        Assert.Empty(residus);
        Assert.True(File.Exists(_paths.SettingsFile));
    }

    [Fact]
    public void Save_cree_le_dossier_manquant()
    {
        // Le dossier %APPDATA%\Chronos n'existe pas encore au départ.
        Assert.False(Directory.Exists(Path.GetDirectoryName(_paths.SettingsFile)!));

        _service.Save(new ChronosSettings());

        Assert.True(File.Exists(_paths.SettingsFile));
    }
}
