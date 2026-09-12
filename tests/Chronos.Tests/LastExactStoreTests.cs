using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve EXA-01 côté persistance : un relevé exact survit au redémarrage du processus, un fichier
/// illisible dégrade en silence, et AUCUN chiffre n'est inventé au rechargement (fraction recalculée,
/// fiabilité dérivée, fenêtre déjà remise à zéro rejetée).
///
/// Isolation stricte : chaque test travaille dans un dossier temp unique — jamais %APPDATA%\Chronos.
/// Tests purs (aucun type WPF, aucune UI) → [Fact] classiques, pas de STA.
/// </summary>
public class LastExactStoreTests : IDisposable
{
    // Horloge figée : tous les instants des tests sont exprimés relativement à celle-ci.
    private static readonly DateTimeOffset Now = new(2026, 09, 09, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir;
    private readonly string _fichier;

    public LastExactStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosLastExact_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _fichier = Path.Combine(_dir, "last-exact.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private static WindowState Exact(WindowKind k, double util, DateTimeOffset resets, DateTimeOffset captured)
        => new()
        {
            Kind = k,
            Utilization = util,
            ResetsAt = resets,
            CapturedAt = captured,
            Reliability = SourceReliability.Exact,
        };

    private static UsageSnapshot Snap(WindowState five, WindowState seven)
        => new() { FiveHour = five, SevenDay = seven };

    // --- 0. EXA-06 : le magasin NOMME ce qu'il reconstruit ---

    /// <summary>
    /// EXA-06 — une fenêtre relue du disque se réclame du MAGASIN, et non du producteur qui avait
    /// fourni le chiffre le jour de sa capture. Le magasin ne persiste pas ce nom (son <c>Entry</c> ne
    /// porte que utilization / resets_at / captured_at) : prétendre le contraire au rechargement
    /// reviendrait à affirmer qu'une source répond alors qu'elle est peut-être tombée depuis des heures.
    ///
    /// C'est ce nom que la doctrine fait ensuite hériter à un plancher par « candidat with » — d'où le
    /// libellé « dernier exact persisté » que l'utilisateur lira dans le diagnostic.
    /// </summary>
    [Fact]
    public void La_fenetre_reconstruite_se_reclame_du_MAGASIN_et_non_du_producteur_d_origine()
    {
        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.42, Now.AddHours(3), Now.AddMinutes(-5)),
            Exact(WindowKind.SevenDay, 0.63, Now.AddDays(4), Now.AddMinutes(-5))));

        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.Equal(SourceUsage.MagasinDernierExact, relu!.FiveHour!.Source);
        Assert.Equal(SourceUsage.MagasinDernierExact, relu.SevenDay!.Source);
    }

    // --- 1. Round-trip : le chiffre survit à la recréation de l'objet magasin ---

    [Fact]
    public void Round_trip_les_deux_fenetres_exactes_reviennent_a_l_identique()
    {
        var capture = Now.AddMinutes(-5);
        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.42, Now.AddHours(3), capture),
            Exact(WindowKind.SevenDay, 0.63, Now.AddDays(4), capture)));

        // NOUVEL objet magasin : rien ne subsiste en RAM, tout vient du disque.
        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.NotNull(relu);
        Assert.NotNull(relu!.FiveHour);
        Assert.Equal(WindowKind.FiveHour, relu.FiveHour!.Kind);
        Assert.Equal(0.42, relu.FiveHour.Utilization);
        Assert.Equal(Now.AddHours(3), relu.FiveHour.ResetsAt);
        Assert.Equal(capture, relu.FiveHour.CapturedAt);
        Assert.Equal(SourceReliability.Exact, relu.FiveHour.Reliability);

        Assert.NotNull(relu.SevenDay);
        Assert.Equal(WindowKind.SevenDay, relu.SevenDay!.Kind);
        Assert.Equal(0.63, relu.SevenDay.Utilization);
        Assert.Equal(Now.AddDays(4), relu.SevenDay.ResetsAt);
        Assert.Equal(capture, relu.SevenDay.CapturedAt);
        Assert.Equal(SourceReliability.Exact, relu.SevenDay.Reliability);
    }

    // --- 2. Deux captured_at distincts : les fenêtres peuvent venir de sources différentes ---

    [Fact]
    public void Deux_captured_at_distincts_sont_restitues_separement()
    {
        var t1 = Now.AddMinutes(-2);
        var t2 = Now.AddHours(-9);

        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.10, Now.AddHours(1), t1),
            Exact(WindowKind.SevenDay, 0.20, Now.AddDays(2), t2)));

        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.Equal(t1, relu!.FiveHour!.CapturedAt);
        Assert.Equal(t2, relu.SevenDay!.CapturedAt);
        Assert.NotEqual(relu.FiveHour.CapturedAt, relu.SevenDay.CapturedAt);
    }

    // --- 3. Une fenêtre non-Exact n'est jamais écrite ---

    [Fact]
    public void Fenetre_non_exacte_ignoree_a_l_ecriture()
    {
        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.42, Now.AddHours(3), Now),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.NotNull(relu!.FiveHour);
        Assert.Null(relu.SevenDay);
    }

    // --- 4. Écriture non destructive : une absence n'efface jamais un chiffre connu ---

    [Fact]
    public void Save_partiel_ne_detruit_pas_la_fenetre_exacte_deja_persistee()
    {
        var store = new LastExactStore(_fichier);
        store.Save(Snap(
            Exact(WindowKind.FiveHour, 0.42, Now.AddHours(3), Now),
            Exact(WindowKind.SevenDay, 0.63, Now.AddDays(4), Now)));

        // Second passage : l'hebdo est tombée (Unavailable) — elle ne doit PAS être effacée.
        store.Save(Snap(
            Exact(WindowKind.FiveHour, 0.55, Now.AddHours(2), Now),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.Equal(0.55, relu!.FiveHour!.Utilization);   // la 5 h est bien mise à jour
        Assert.NotNull(relu.SevenDay);                     // l'hebdo précédente a SURVÉCU
        Assert.Equal(0.63, relu.SevenDay!.Utilization);
    }

    // --- 5/6/7. Lecture tolérante : jamais d'exception, jamais de valeur inventée ---

    [Fact]
    public void Fichier_absent_rend_null_sans_exception()
        => Assert.Null(new LastExactStore(_fichier).Load(Now));

    [Fact]
    public void Fichier_corrompu_rend_null_sans_exception()
    {
        File.WriteAllText(_fichier, "{ ceci n est pas du JSON ]");
        Assert.Null(new LastExactStore(_fichier).Load(Now));
    }

    [Fact]
    public void Version_de_schema_inconnue_rend_null_sans_exception()
    {
        File.WriteAllText(_fichier,
            "{\"version\":999,\"five_hour\":{\"utilization\":0.5," +
            "\"resets_at\":\"2026-09-09T15:00:00+00:00\",\"captured_at\":\"2026-09-09T11:00:00+00:00\"}}");

        Assert.Null(new LastExactStore(_fichier).Load(Now));
    }

    // --- 8. Garde d'honnêteté : une fenêtre déjà remise à zéro ne décrit plus rien ---

    [Fact]
    public void Fenetre_roulee_rejetee_au_chargement_l_autre_survit()
    {
        // Save ne filtre pas (la fusion ne doit pas purger) ; c'est Load qui invalide.
        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.80, Now.AddMinutes(-1), Now.AddHours(-6)),
            Exact(WindowKind.SevenDay, 0.63, Now.AddDays(4), Now.AddHours(-6))));

        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.NotNull(relu);
        Assert.Null(relu!.FiveHour);        // reset passé → aucun pourcentage ressuscité
        Assert.NotNull(relu.SevenDay);      // l'autre fenêtre, encore valide, est restituée
        Assert.Equal(0.63, relu.SevenDay!.Utilization);
    }

    [Fact]
    public void Toutes_les_fenetres_roulees_rendent_null()
    {
        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.80, Now.AddMinutes(-1), Now.AddHours(-6)),
            Exact(WindowKind.SevenDay, 0.63, Now.AddDays(-1), Now.AddDays(-8))));

        Assert.Null(new LastExactStore(_fichier).Load(Now));
    }

    // --- 9/10. La fraction de temps restante est RECALCULÉE, jamais relue du disque ---

    [Fact]
    public void Fraction_recalculee_meme_si_absente_du_fichier()
    {
        // 15:00 - 12:00 = 3 h restantes sur une fenêtre de 5 h → 0,6.
        File.WriteAllText(_fichier,
            "{\"version\":1,\"five_hour\":{\"utilization\":0.5," +
            "\"resets_at\":\"2026-09-09T15:00:00+00:00\",\"captured_at\":\"2026-09-09T11:00:00+00:00\"}}");

        var relu = new LastExactStore(_fichier).Load(Now);

        Assert.NotNull(relu!.FiveHour!.FractionTimeRemaining);
        Assert.Equal(0.6, relu.FiveHour.FractionTimeRemaining!.Value, 3);
    }

    [Fact]
    public void Fraction_presente_dans_le_json_est_ignoree()
    {
        File.WriteAllText(_fichier,
            "{\"version\":1,\"five_hour\":{\"utilization\":0.5,\"fraction_time_remaining\":0.99," +
            "\"resets_at\":\"2026-09-09T15:00:00+00:00\",\"captured_at\":\"2026-09-09T11:00:00+00:00\"}}");

        var relu = new LastExactStore(_fichier).Load(Now);

        // 0,99 vient du fichier ; la valeur honnête est le recalcul 0,6.
        Assert.Equal(0.6, relu!.FiveHour!.FractionTimeRemaining!.Value, 3);
    }

    // --- 11. Atomicité : aucun temporaire résiduel ---

    [Fact]
    public void Aucun_fichier_temporaire_ne_subsiste_apres_save()
    {
        new LastExactStore(_fichier).Save(Snap(
            Exact(WindowKind.FiveHour, 0.42, Now.AddHours(3), Now),
            Exact(WindowKind.SevenDay, 0.63, Now.AddDays(4), Now)));

        Assert.Empty(Directory.GetFiles(_dir, "last-exact.json.tmp-*"));
        Assert.Single(Directory.GetFiles(_dir));
        Assert.True(File.Exists(_fichier));
    }

    // --- 12. Isolation : le chemin vient de ChronosPaths, jamais de %APPDATA% en dur ---

    [Fact]
    public void LastExactFile_reste_dans_le_dossier_du_usage_file_injecte()
    {
        var paths = new ChronosPaths(Path.Combine(_dir, "usage.json"), Path.Combine(_dir, "projects"));

        Assert.Equal(Path.Combine(_dir, "last-exact.json"), paths.LastExactFile);
        Assert.StartsWith(_dir, paths.LastExactFile);
    }

    // --- EXA-05 : « jamais rien vu » n'est PAS « vu, mais la fenêtre a roulé » ---

    [Fact]
    public void Aucun_fichier_signifie_qu_aucun_exact_n_a_JAMAIS_ete_obtenu()
    {
        Assert.False(new LastExactStore(_fichier).UnExactADejaEteObtenu());
    }

    [Fact]
    public void Un_releve_dont_la_fenetre_a_roule_prouve_tout_de_meme_qu_un_exact_a_ete_obtenu()
    {
        var magasin = new LastExactStore(_fichier);
        magasin.Save(Snap(
            Exact(WindowKind.FiveHour, 0.42, Now.AddMinutes(-1), Now.AddHours(-6)),   // reset DÉJÀ passé
            WindowState.Unavailable(WindowKind.SevenDay)));

        Assert.Null(magasin.Load(Now));                  // plus rien de servable…
        Assert.True(magasin.UnExactADejaEteObtenu());    // …mais l'utilisateur N'EST PAS « jamais connecté »
    }

    [Fact]
    public void Un_fichier_illisible_ne_permet_d_affirmer_aucun_exact_obtenu()
    {
        File.WriteAllText(_fichier, "{ pas du JSON ]");
        Assert.False(new LastExactStore(_fichier).UnExactADejaEteObtenu());
    }

    [Fact]
    public void Une_fenetre_exacte_SANS_instant_de_capture_n_est_pas_persistee()
    {
        var sansCapture = new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
            Utilization = 0.42, ResetsAt = Now.AddHours(3), CapturedAt = null,
        };

        new LastExactStore(_fichier).Save(Snap(sansCapture, WindowState.Unavailable(WindowKind.SevenDay)));

        Assert.False(File.Exists(_fichier));   // rien de certifiable => pas même un fichier
    }
}
