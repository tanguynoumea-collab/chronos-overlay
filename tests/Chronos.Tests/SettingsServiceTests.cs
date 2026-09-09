using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Placement;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la persistance atomique/tolérante de settings.json (FEN-07) : round-trip, défauts sur
/// fichier absent, défauts SANS exception sur JSON corrompu, écriture atomique (pas de .tmp
/// résiduel) et création du dossier. Chaque test isole un répertoire temp injecté via
/// <c>new ChronosPaths(usage, projects)</c> (ctor positionnel inchangé) → aucun accès au vrai profil.
///
/// Prouve AUSSI la compatibilité ascendante DEL-06 (phase 16) sur une fixture FIGÉE du vrai
/// %APPDATA%\Chronos\settings.json de production, portant les six champs de plafonds supprimés :
/// aucun code de migration n'existe ni n'est nécessaire — System.Text.Json ignore par défaut les
/// membres JSON non mappés (JsonUnmappedMemberHandling.Skip), et le skip a lieu au niveau du
/// LECTEUR, avant toute tentative de conversion (d'où l'innocuité d'un type incohérent ou d'une
/// valeur d'enum devenue invalide).
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

    // ---------------------------------------------------------------------------------------
    // DEL-06 — compatibilité ascendante avec le settings.json d'AVANT la démolition des plafonds.
    // ---------------------------------------------------------------------------------------

    // Chemin de la fixture résolu à la COMPILATION ([CallerFilePath]) — motif déjà en place dans
    // TranscriptActivityProviderTests / ClaudeUsageObjectProviderTests, aucun couplage au .csproj.
    private static string TestDataPath(string file, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData", file);

    /// <summary>Texte LITTÉRAL du settings.json réel de production figé au 2026-09-09 : 6 champs de
    /// plafonds obsolètes + les 18 préférences survivantes. Jamais régénéré par sérialisation — le
    /// code actuel ne sait plus produire les champs obsolètes, une fixture générée ne prouverait rien.</summary>
    private static string FixtureLegacy() => File.ReadAllText(TestDataPath("settings-legacy-plafonds.json"));

    /// <summary>Dépose un contenu JSON à l'emplacement settings.json du dossier temp isolé du test.</summary>
    private void EcrireSettings(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsFile)!);
        File.WriteAllText(_paths.SettingsFile, json);
    }

    /// <summary>
    /// LE test DEL-06. Le settings.json de production (v2.8.1, portant les six champs de plafonds)
    /// s'ouvre sans erreur avec le schéma amputé, et les DIX-HUIT préférences de l'utilisateur sont
    /// restituées à l'identique — y compris l'offset +02:00 de l'ancre hebdo et les styles de la
    /// refonte visuelle. C'est la preuve qu'aucun migrateur n'est nécessaire.
    /// </summary>
    [Fact]
    public void Reglages_avec_anciens_plafonds_s_ouvrent_sans_erreur_et_conservent_les_18_preferences()
    {
        EcrireSettings(FixtureLegacy());

        var s = _service.Load(); // ne doit PAS lever

        Assert.Equal(OverlayCorner.BottomRight, s.Corner);
        Assert.Equal(@"\\.\DISPLAY2", s.MonitorDeviceName);
        Assert.Equal(-182.4, s.X);
        Assert.Equal(921.6, s.Y);
        Assert.False(s.Background);
        Assert.Equal(60, s.RefreshIntervalSeconds);
        Assert.Equal(new DateTimeOffset(2026, 07, 11, 00, 00, 00, TimeSpan.FromHours(2)), s.WeeklyAnchor);
        Assert.True(s.OAuthUsageEnabled);
        Assert.Null(s.InnerStatusLineCommand);
        Assert.False(s.StatusLinePromptDismissed);
        Assert.Equal("ardoise", s.ThemeKey);
        Assert.True(s.SessionsWidgetEnabled);
        Assert.Equal(-77.60000000000001, s.SessionsX);
        Assert.Equal(107.2, s.SessionsY);
        Assert.Equal(CadranDisplayMode.Normal, s.CadranMode);
        Assert.Equal(CadranStyle.Arcs, s.CadranStyle);
        Assert.Equal(SessionStyle.Veilleurs, s.SessionStyle);
        Assert.True(s.VerticalLayout);
    }

    /// <summary>
    /// Purge PASSIVE : la disparition des six champs obsolètes du fichier n'est le fait d'aucun code
    /// dédié — elle survient au premier Save() (déplacement de l'overlay, changement de thème, tout
    /// toggle), parce que le type sérialisé ne les porte plus.
    /// </summary>
    [Fact]
    public void Les_six_champs_obsoletes_disparaissent_au_premier_Save()
    {
        EcrireSettings(FixtureLegacy());

        _service.Save(_service.Load());

        var texte = File.ReadAllText(_paths.SettingsFile);
        Assert.DoesNotContain("FiveHourTokenBudget", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("WeeklyTokenBudget", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("FiveHourBudgetSource", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("WeeklyBudgetSource", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("FiveHourBudgetCalibratedAt", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("WeeklyBudgetCalibratedAt", texte, StringComparison.Ordinal);

        // …sans emporter les préférences survivantes au passage.
        Assert.Contains("ThemeKey", texte, StringComparison.Ordinal);
        Assert.Contains("SessionStyle", texte, StringComparison.Ordinal);
    }

    /// <summary>
    /// Un champ supprimé dont la VALEUR a un type incohérent (objet là où on attendait un nombre)
    /// n'est même pas converti : le lecteur saute le membre non mappé entier, sous-arbre compris.
    /// </summary>
    [Fact]
    public void Champ_obsolete_de_type_incoherent_est_ignore()
    {
        EcrireSettings(FixtureLegacy().Replace(
            "\"FiveHourTokenBudget\": 230000000",
            "\"FiveHourTokenBudget\": {\"a\":[1,2,3]}",
            StringComparison.Ordinal));

        var s = _service.Load(); // ne doit PAS lever

        Assert.Equal("ardoise", s.ThemeKey);
        Assert.Equal(OverlayCorner.BottomRight, s.Corner);
    }

    /// <summary>
    /// Même une valeur d'enum SUPPRIMÉ devenue invalide (« NimporteQuoi » là où l'enum BudgetSource
    /// n'existe plus) est inoffensive : le JsonStringEnumConverter n'est jamais sollicité pour un
    /// membre que le type cible ne déclare pas.
    /// </summary>
    [Fact]
    public void Valeur_d_enum_supprimee_invalide_est_ignoree()
    {
        EcrireSettings(FixtureLegacy().Replace(
            "\"FiveHourBudgetSource\": \"Manual\"",
            "\"FiveHourBudgetSource\": \"NimporteQuoi\"",
            StringComparison.Ordinal));

        var s = _service.Load(); // ne doit PAS lever

        Assert.Equal(OverlayCorner.BottomRight, s.Corner);
    }
}
