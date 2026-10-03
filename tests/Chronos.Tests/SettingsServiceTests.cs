using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Chronos.Placement;
using Chronos.Services;
using Chronos.Theming;
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
            SondeEnTetesActivee = false, // valeur ≠ défaut pour prouver la persistance d'un interrupteur (HDR-06)
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
        Assert.True(s.SondeEnTetesActivee); // défaut true : vrais chiffres dès l'installation (HDR-06)
    }

    /// <summary>DAT-02 (37-03) — le réglage du jeton de l'app bureau est retiré du schéma. Un settings.json qui le porte
    /// encore se charge sans erreur : membre inconnu ignoré, les autres préférences conservées (tolérance de la phase 36).</summary>
    [Fact]
    public void Un_ancien_OAuthUsageEnabled_est_ignore_sans_perdre_le_reste()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsFile)!);
        File.WriteAllText(_paths.SettingsFile, "{\"OAuthUsageEnabled\": false, \"ThemeKey\": \"nord\"}");

        var s = _service.Load(); // ne doit PAS lever

        Assert.Equal("nord", s.ThemeKey);
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
    // TranscriptActivityProviderTests, aucun couplage au .csproj.
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
    /// refonte visuelle. C'est la preuve qu'aucun migrateur n'est nécessaire. (Depuis 37-05, les deux préférences de la
    /// barre de statut retirée sont à leur tour des membres inconnus : ignorées sans erreur, comme les plafonds.)
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
        // « OAuthUsageEnabled » est encore dans la fixture : depuis 37-03 c'est un membre inconnu, ignoré sans erreur.
        // Idem depuis 37-05 pour les deux clés de la barre de statut (commande chaînée, proposition écartée) : ignorées.
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

    // ---------------------------------------------------------------------------------------
    // Phase 34 (HIS-08) — géométrie de la fenêtre Historique (le style de la vue Semaine est supprimé en phase 38).
    // ---------------------------------------------------------------------------------------

    /// <summary>Un settings.json d'AVANT la fenêtre Historique s'ouvre avec les défauts sûrs : aucune géométrie mémorisée
    /// (null = 920 × 610 centré) — aucune migration.</summary>
    [Fact]
    public void La_geometrie_de_l_historique_a_des_defauts_surs()
    {
        EcrireSettings(FixtureLegacy());

        var s = _service.Load();

        Assert.Null(s.HistoriqueX);
        Assert.Null(s.HistoriqueY);
        Assert.Null(s.HistoriqueWidth);
        Assert.Null(s.HistoriqueHeight);
    }

    /// <summary>La géométrie de la fenêtre Historique fait l'aller-retour à l'identique.</summary>
    [Fact]
    public void La_geometrie_de_l_historique_fait_l_aller_retour()
    {
        var original = _service.Load() with
        {
            HistoriqueX = 100,
            HistoriqueY = 50,
            HistoriqueWidth = 900,
            HistoriqueHeight = 600,
        };

        _service.Save(original);
        var relu = _service.Load();

        Assert.Equal(original, relu);
        Assert.Equal(100, relu.HistoriqueX);
        Assert.Equal(600, relu.HistoriqueHeight);
    }

    // ---------------------------------------------------------------------------------------
    // Phase 38 (HIS-09) — HistoriqueStyleSemaine supprimée.
    // ---------------------------------------------------------------------------------------

    /// <summary>Témoin de la suppression : un ancien settings.json qui porte encore le style de la vue Semaine (quelle que
    /// soit sa valeur, y compris Tuiles) se lit SANS PERTE — thème, coin, moniteur, mode du cadran et les 10 géométries
    /// relus à l'identique. Le membre est désormais inconnu, donc ignoré (pas une retombée) ; il disparaît au premier Save.</summary>
    [Theory]
    [InlineData("Tuiles")]
    [InlineData("Simplifie")]
    [InlineData("Pistes")]
    public void Un_ancien_reglage_de_style_d_historique_se_lit_sans_perte(string valeur)
    {
        EcrireSettings($$"""
            {
              "ThemeKey": "nord",
              "Corner": "BottomLeft",
              "MonitorDeviceName": "\\\\.\\DISPLAY2",
              "CadranMode": "Etendu",
              "HistoriqueStyleSemaine": "{{valeur}}",
              "HistoriqueX": 100, "HistoriqueY": 120, "HistoriqueWidth": 1000, "HistoriqueHeight": 700,
              "ReglagesX": 200, "ReglagesY": 220, "ReglagesWidth": 900, "ReglagesHeight": 600,
              "SessionsX": 10, "SessionsY": 20
            }
            """);

        var s = _service.Load();

        Assert.Equal("nord", s.ThemeKey);
        Assert.Equal(OverlayCorner.BottomLeft, s.Corner);
        Assert.Equal(@"\\.\DISPLAY2", s.MonitorDeviceName);
        Assert.Equal(CadranDisplayMode.Etendu, s.CadranMode);
        Assert.Equal(100, s.HistoriqueX);
        Assert.Equal(120, s.HistoriqueY);
        Assert.Equal(1000, s.HistoriqueWidth);
        Assert.Equal(700, s.HistoriqueHeight);
        Assert.Equal(200, s.ReglagesX);
        Assert.Equal(220, s.ReglagesY);
        Assert.Equal(900, s.ReglagesWidth);
        Assert.Equal(600, s.ReglagesHeight);
        Assert.Equal(10, s.SessionsX);
        Assert.Equal(20, s.SessionsY);
        Assert.Equal(IssueLectureReglages.Lu, _service.DerniereLecture.Issue);
        Assert.Empty(_service.DernieresRetombees);   // un membre inconnu n'est pas une retombée

        _service.Save(s);
        Assert.DoesNotContain("HistoriqueStyleSemaine", File.ReadAllText(_paths.SettingsFile), StringComparison.Ordinal);
        Assert.Equal(s, _service.Load());
    }

    [Fact]
    public void La_propriete_HistoriqueStyleSemaine_n_existe_plus()
    {
        Assert.Null(typeof(ChronosSettings).GetProperty("HistoriqueStyleSemaine"));
    }

    // SOC-01 (phase 36) — lecture tolérante valeur par valeur

    private static string FixtureValeursInconnues() => File.ReadAllText(TestDataPath("settings-valeurs-inconnues.json"));

    /// <summary>Une valeur fautive ne coûte qu'elle-même : les trois enums inconnus retombent sur LEUR défaut, tout le reste
    /// (thème, coin, moniteur, mode, 10 géométries) est conservé ; les membres inconnus (dont la commande chaînée de la barre
    /// de statut, retirée en 37-05, et le style de la vue Semaine, supprimé en phase 38) sont ignorés.</summary>
    [Fact]
    public void Fixture_valeurs_inconnues_ne_coute_que_les_valeurs_fautives()
    {
        EcrireSettings(FixtureValeursInconnues());

        var s = _service.Load();

        Assert.Equal("nord", s.ThemeKey);
        Assert.Equal(OverlayCorner.BottomLeft, s.Corner);
        Assert.Equal(@"\\.\DISPLAY2", s.MonitorDeviceName);
        Assert.Equal(CadranDisplayMode.Etendu, s.CadranMode);
        Assert.Equal(100, s.HistoriqueX);
        Assert.Equal(120, s.HistoriqueY);
        Assert.Equal(1000, s.HistoriqueWidth);
        Assert.Equal(700, s.HistoriqueHeight);
        Assert.Equal(200, s.ReglagesX);
        Assert.Equal(220, s.ReglagesY);
        Assert.Equal(900, s.ReglagesWidth);
        Assert.Equal(600, s.ReglagesHeight);
        Assert.Equal(10, s.SessionsX);
        Assert.Equal(20, s.SessionsY);

        Assert.Equal(CadranStyle.Arcs, s.CadranStyle);
        Assert.Equal(SessionStyle.Pastilles, s.SessionStyle);
        Assert.Equal(SectionReglages.Donnees, s.ReglagesSection);

        Assert.Equal(IssueLectureReglages.LuAvecRetombees, _service.DerniereLecture.Issue);
        Assert.Equal(new[] { "CadranStyle", "ReglagesSection", "SessionStyle" },
            _service.DernieresRetombees.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>Save(Load()) grave les valeurs conservées à l'identique, remplace les fautives par leur défaut et fait
    /// disparaître les membres inconnus (dont la commande chaînée héritée : le réconciliateur la lit AVANT tout Save) ; la
    /// relecture suivante ne signale plus rien.</summary>
    [Fact]
    public void Save_apres_retombees_reecrit_le_fichier_sans_perte()
    {
        EcrireSettings(FixtureValeursInconnues());

        var premier = _service.Load();
        _service.Save(premier);
        var texte = File.ReadAllText(_paths.SettingsFile);

        Assert.Contains("nord", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("echo hi", texte, StringComparison.Ordinal);   // clé héritée ≤ 3.4 : membre inconnu depuis 37-05
        Assert.Contains("\"CadranStyle\": \"Arcs\"", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("Spirale", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("Fantome", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("Mosaique", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("Inconnu", texte, StringComparison.Ordinal);
        Assert.DoesNotContain("OrientationCadran", texte, StringComparison.Ordinal);

        var relu = _service.Load();
        Assert.Equal(IssueLectureReglages.Lu, _service.DerniereLecture.Issue);
        Assert.Empty(_service.DernieresRetombees);
        Assert.Equal(premier, relu);
    }

    /// <summary>Piège : un convertisseur rendant default(T) remettrait Corner à TopLeft (index 0). La retombée prend
    /// l'initialiseur de la propriété : TopRight.</summary>
    [Theory]
    [InlineData("\"Fantome\"")]
    [InlineData("99")]
    [InlineData("null")]
    public void Corner_invalide_retombe_sur_TopRight_et_non_sur_l_index_0(string valeur)
    {
        EcrireSettings("{\"ThemeKey\":\"nord\",\"Corner\":" + valeur + "}");

        var s = _service.Load();

        Assert.Equal(OverlayCorner.TopRight, s.Corner);
        Assert.Equal("nord", s.ThemeKey);
        Assert.Equal(new[] { "Corner" }, _service.DernieresRetombees);
    }

    /// <summary>Mécanisme GÉNÉRIQUE : chaque propriété enum de ChronosSettings (énumérée par réflexion, y compris un enum
    /// futur) retombe sur SA valeur par défaut sans toucher aux autres.</summary>
    [Fact]
    public void Chaque_enum_des_reglages_retombe_sur_son_propre_defaut()
    {
        var proprietes = typeof(ChronosSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsEnum)
            .ToList();
        Assert.True(proprietes.Count >= 5); // garde muette : la réflexion trouve bien les enums

        var defauts = new ChronosSettings();
        foreach (var p in proprietes)
        {
            EcrireSettings("{\"ThemeKey\":\"nord\",\"MonitorDeviceName\":\"ECRAN2\",\"" + p.Name + "\":\"Fantome\"}");

            var lu = _service.Load();

            Assert.Equal(p.GetValue(defauts), p.GetValue(lu));
            Assert.Equal("nord", lu.ThemeKey);
            Assert.Equal("ECRAN2", lu.MonitorDeviceName);
            Assert.Equal(new[] { p.Name }, _service.DernieresRetombees);
        }
    }

    /// <summary>Depuis la phase 38, « Tuiles » est la valeur d'un membre inconnu ignoré (le style de la vue Semaine est
    /// supprimé) : il ne coûte jamais les autres réglages.</summary>
    [Fact]
    public void Tuiles_ne_coute_jamais_les_autres_reglages()
    {
        EcrireSettings(FixtureValeursInconnues().Replace("\"Mosaique\"", "\"Tuiles\""));

        var s = _service.Load();

        Assert.Equal("nord", s.ThemeKey);
        Assert.Equal(OverlayCorner.BottomLeft, s.Corner);
        Assert.Equal(@"\\.\DISPLAY2", s.MonitorDeviceName);
        Assert.Equal(1000, s.HistoriqueWidth);
        Assert.Equal(600, s.ReglagesHeight);
    }

    /// <summary>JSON réellement illisible (tronqué, non-JSON, racine tableau / null / scalaire) → défauts ENTIERS.</summary>
    [Theory]
    [InlineData("{\"ThemeKey\":\"nord\",\"Corner\":\"BottomLeft\"")]
    [InlineData("{ ceci n'est pas du JSON valide ]")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("\"texte\"")]
    public void Json_illisible_redonne_les_defauts_entiers_jamais_a_moitie_lu(string texte)
    {
        EcrireSettings(texte);

        var s = _service.Load();

        Assert.Equal(new ChronosSettings(), s);
        Assert.Equal("minuit", s.ThemeKey);
        Assert.Equal(IssueLectureReglages.Illisible, _service.DerniereLecture.Issue);
    }

    /// <summary>Les valeurs non-enum mal typées retombent aussi, chacune seule.</summary>
    [Fact]
    public void Valeur_non_enum_mal_typee_retombe_seule()
    {
        EcrireSettings("{\"ThemeKey\":\"nord\",\"RefreshIntervalSeconds\":\"abc\",\"Background\":null,\"WeeklyAnchor\":\"pas une date\"}");

        var s = _service.Load();

        Assert.Equal(60, s.RefreshIntervalSeconds);
        Assert.False(s.Background);
        Assert.Null(s.WeeklyAnchor);
        Assert.Equal("nord", s.ThemeKey);
        Assert.Contains("RefreshIntervalSeconds", _service.DernieresRetombees);
        Assert.Contains("Background", _service.DernieresRetombees);
        Assert.Contains("WeeklyAnchor", _service.DernieresRetombees);
    }

    /// <summary>ThemeKey est non-nullable : null retombe sur « minuit » sans toucher au coin.</summary>
    [Fact]
    public void ThemeKey_null_retombe_sur_minuit()
    {
        EcrireSettings("{\"ThemeKey\":null,\"Corner\":\"BottomLeft\"}");

        var s = _service.Load();

        Assert.Equal("minuit", s.ThemeKey);
        Assert.Equal(OverlayCorner.BottomLeft, s.Corner);
        Assert.Equal(new[] { "ThemeKey" }, _service.DernieresRetombees);
    }

    /// <summary>Une clé de thème inconnue est conservée brute (SettingsService ne connaît pas le catalogue, qui est WPF) ;
    /// ThemeCatalog.ByKey la fait retomber sur minuit à l'affichage.</summary>
    [Fact]
    public void Cle_de_theme_inconnue_est_conservee_et_retombe_a_l_affichage()
    {
        EcrireSettings("{\"ThemeKey\":\"theme-disparu\",\"Corner\":\"BottomLeft\"}");

        var s = _service.Load();

        Assert.Equal("theme-disparu", s.ThemeKey);
        Assert.Equal(OverlayCorner.BottomLeft, s.Corner);
        Assert.Equal(IssueLectureReglages.Lu, _service.DerniereLecture.Issue);
        Assert.Equal("minuit", ThemeCatalog.ByKey(s.ThemeKey).Key);
    }

    /// <summary>Fichier absent → Absent ; fichier sain écrit par Save → Lu, aucune retombée.</summary>
    [Fact]
    public void Fichier_absent_ou_sain_ne_signale_aucune_retombee()
    {
        _service.Load();
        Assert.Equal(IssueLectureReglages.Absent, _service.DerniereLecture.Issue);

        _service.Save(new ChronosSettings { ThemeKey = "nord", Corner = OverlayCorner.BottomLeft });
        _service.Load();

        Assert.Equal(IssueLectureReglages.Lu, _service.DerniereLecture.Issue);
        Assert.Empty(_service.DernieresRetombees);
    }
}
