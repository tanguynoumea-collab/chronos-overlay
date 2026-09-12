using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Chronos.Models;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;
using WindowState = Chronos.Models.WindowState; // lève l'ambiguïté avec System.Windows.WindowState

namespace Chronos.Tests;

/// <summary>
/// Smoke test BAML de la fenêtre de RÉGLAGES (HDR-06, HDR-03/HDR-04).
///
/// Ce que ces tests attrapent et que <c>dotnet build</c> ne voit pas :
/// <list type="bullet">
///   <item>l'interrupteur de la sonde bindé sur la MAUVAISE commande — compile parfaitement, et un clic
///         couperait la source gratuite (<c>ToggleOAuthUsage</c>) ou, pire, supprimerait le coffre de
///         jetons (<c>LoginClaude</c>) au lieu de couper la seule source qui dépense ;</item>
///   <item>le libellé de coût retiré ou amputé de son chiffre : HDR-06 exige que le coût soit ÉCRIT, et
///         une promesse d'honnêteté sans test est une promesse qu'un refactor efface en silence ;</item>
///   <item>la ligne d'état serveur visible alors que rien n'est rapporté — un cadre vide qui a l'air
///         d'une information.</item>
/// </list>
///
/// L'appartenance à la collection sérialisée est OBLIGATOIRE : cette classe charge du BAML, et le
/// chargeur XAML de WPF a une course connue (voir <see cref="XamlWpfCollection"/>) qui fait lever un
/// <c>XamlParseException</c> INTERMITTENT sur un XAML pourtant valide.
/// </summary>
[Collection("XAML WPF")]
public class ReglagesBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Chemins de test sous <c>Path.GetTempPath()</c> : AUCUN test n'écrit dans le vrai
    /// <c>%APPDATA%\Chronos</c>, ni ne lit le coffre de jetons réel.</summary>
    private static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosReglagesTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.StartsWith(Path.GetTempPath(), dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    /// <summary>
    /// Monte la fenêtre de réglages dans l'état voulu et met en page sa GRILLE RACINE.
    ///
    /// Une Window jamais affichée n'a pas de template appliqué : son <c>Content</c> n'a AUCUN parent
    /// visuel, donc le DataContext ne se propage pas et AUCUN binding ne s'évalue (<c>Command</c> reste
    /// null, <c>Visibility</c> reste à son défaut <c>Visible</c>) — les tests seraient verts pour de
    /// mauvaises raisons. D'où : DataContext posé sur la racine du contenu, purge de la file du
    /// Dispatcher (la réévaluation déclenchée par un changement de DataContext est DIFFÉRÉE), puis
    /// Measure/Arrange. Motif du helper <c>MonterPastille</c> de <see cref="CadranBindingTests"/>.
    /// </summary>
    private static (SettingsWindow fenetre, MainViewModel vm) MonterReglages(
        bool sondeActivee, StatutServeur? statutCinqHeures = null)
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        settings.Save(settings.Load() with { SondeEnTetesActivee = sondeActivee });

        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, paths, RefreshOptions.Default); // JAMAIS démarré : aucun I/O
        var clock = new FakeClock(Now);
        var vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(),
            settings,
            new DiagnosticService(new FakeClaudeTokenReader(), paths, settings, provider, clock),
            new FakeStatusLineSetup(), new FakeOAuthLogin(), new FakeSessionsController(),
            new FakeAuthStatus(), new FakeEtatServeur());

        // Le statut est appliqué AVANT le montage : les bindings s'évaluent alors une seule fois, sur
        // l'état final, et le test ne dépend pas d'un second aller-retour de Dispatcher.
        if (statutCinqHeures is not null)
            vm.ApplySnapshot(new UsageSnapshot
            {
                FiveHour = new WindowState
                {
                    Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.97,
                    ResetsAt = Now + TimeSpan.FromMinutes(20), StatutServeur = statutCinqHeures,
                },
                SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
                SourceCapturedAt = Now,
            });

        var fenetre = new SettingsWindow(vm);
        var racine = (FrameworkElement)fenetre.Content!;
        racine.DataContext = vm;
        racine.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        racine.Measure(new Size(400, 1400));
        racine.Arrange(new Rect(0, 0, 400, 1400));
        return (fenetre, vm);
    }

    /// <summary>Parcourt l'arbre VISUEL (seul peuplé après Arrange) et rend tous les TextBlock.</summary>
    private static IEnumerable<TextBlock> TousLesTextBlocks(DependencyObject racine)
    {
        if (racine is TextBlock tb) yield return tb;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var t in TousLesTextBlocks(VisualTreeHelper.GetChild(racine, i)))
                yield return t;
    }

    /// <summary>
    /// HDR-06 — LE piège du plan, verrouillé au niveau du XAML et non du seul ViewModel : les deux
    /// interrupteurs de la section DONNÉES ne doivent pas pouvoir être confondus au câblage. Bindé sur
    /// <c>ToggleOAuthUsageCommand</c>, un clic couperait la source qui ne coûte RIEN en laissant la sonde
    /// dépenser ; bindé sur <c>LoginClaudeCommand</c>, il supprimerait le coffre de jetons.
    /// </summary>
    [WpfFact]
    public void L_interrupteur_de_sonde_est_binde_sur_SA_commande_et_non_sur_une_autre()
    {
        var (fenetre, vm) = MonterReglages(sondeActivee: true);
        var interrupteur = Assert.IsType<ToggleButton>(fenetre.FindName("InterrupteurSonde"));

        Assert.Same(vm.ToggleSondeEnTetesCommand, interrupteur.Command);
        Assert.NotSame(vm.ToggleOAuthUsageCommand, interrupteur.Command);
        Assert.NotSame(vm.LoginClaudeCommand, interrupteur.Command);
    }

    /// <summary>HDR-06 — l'interrupteur est un MIROIR de l'état persisté, dans les deux sens. Bindé sur
    /// une autre propriété booléenne (IsOAuthUsageEnabled, par défaut true), la branche « coupée »
    /// tomberait.</summary>
    [WpfFact]
    public void L_interrupteur_reflete_l_etat_persiste()
    {
        var (coupee, vmCoupee) = MonterReglages(sondeActivee: false);
        var interrupteurCoupe = Assert.IsType<ToggleButton>(coupee.FindName("InterrupteurSonde"));
        Assert.False(vmCoupee.IsSondeEnTetesActivee);
        Assert.False(interrupteurCoupe.IsChecked);

        var (active, vmActive) = MonterReglages(sondeActivee: true);
        var interrupteurActif = Assert.IsType<ToggleButton>(active.FindName("InterrupteurSonde"));
        Assert.True(vmActive.IsSondeEnTetesActivee);
        Assert.True(interrupteurActif.IsChecked);
    }

    /// <summary>
    /// HDR-06 — le coût est ÉCRIT, noir sur blanc, là où l'utilisateur décide. La sonde est la seule
    /// source du projet qui dépense du quota pour en mesurer : le cacher ferait de Chronos un outil qui
    /// prélève sans le dire. Si quelqu'un retire le libellé ou son chiffre, ce test tombe.
    /// </summary>
    [WpfFact]
    public void Le_cout_de_la_sonde_est_ECRIT_dans_les_reglages()
    {
        var (fenetre, _) = MonterReglages(sondeActivee: true);
        var racine = (FrameworkElement)fenetre.Content!;

        var libelle = TousLesTextBlocks(racine)
            .Select(t => t.Text ?? "")
            .FirstOrDefault(t => t.Contains("micro-requête") && t.Contains("288"));

        Assert.False(string.IsNullOrEmpty(libelle),
                     "le coût de la sonde doit être annoncé dans les réglages (micro-requête + ≈ 288/jour)");
    }

    /// <summary>
    /// HDR-03/HDR-04 — rien de rapporté n'affiche RIEN. Vérifié sur la Visibility RÉSOLUE : sans le
    /// montage ci-dessus, elle resterait à son défaut <c>Visible</c> et le test serait vert pour de
    /// mauvaises raisons (piège vécu au plan 17-05).
    /// </summary>
    [WpfFact]
    public void La_ligne_d_etat_serveur_est_masquee_quand_rien_n_est_rapporte()
    {
        var (muette, vmMuet) = MonterReglages(sondeActivee: true);
        var ligneMuette = Assert.IsType<TextBlock>(muette.FindName("LigneEtatSonde"));
        Assert.False(vmMuet.AfficherEtatSonde);
        Assert.Equal(Visibility.Collapsed, ligneMuette.Visibility);

        var (parlante, vmParlant) = MonterReglages(sondeActivee: true,
                                                   statutCinqHeures: StatutServeur.AutoriseAvertissement);
        var ligneParlante = Assert.IsType<TextBlock>(parlante.FindName("LigneEtatSonde"));
        Assert.True(vmParlant.AfficherEtatSonde);
        Assert.Equal(Visibility.Visible, ligneParlante.Visibility);
        Assert.Contains("AUTORISÉ (avertissement)", ligneParlante.Text);
    }

    /// <summary>
    /// Le trou de la grille des réglages (phase 20). L'ancien conteneur était une <c>UniformGrid</c>
    /// à 2 colonnes pour 3 boutons : depuis le retrait du bouton « Plafonds… » (phase 16), la cellule
    /// bas-droite était VIDE. Le piège à connaître : <c>UniformGrid</c> ignore SILENCIEUSEMENT
    /// <c>Grid.ColumnSpan</c> — annoter l'enfant compile, s'affiche sans erreur et ne fait RIEN. Seul
    /// le remplacement du conteneur corrige la mise en page.
    ///
    /// D'où une assertion sur la LARGEUR MESURÉE et non sur la présence de l'attribut : un
    /// <c>ColumnSpan</c> ignoré donnerait un rapport de largeurs ≈ 1, jamais &gt; 1,8. Aucun
    /// <c>dotnet build</c> n'attrape cela.
    /// </summary>
    [WpfFact]
    public void Le_bouton_Diagnostic_occupe_toute_la_largeur_et_aucun_libelle_n_est_tronque()
    {
        var (fenetre, _) = MonterReglages(sondeActivee: false);

        var diagnostic  = Assert.IsType<Button>(fenetre.FindName("BoutonDiagnostic"));
        var recalibrer  = Assert.IsType<Button>(fenetre.FindName("BoutonRecalibrerHebdo"));
        var sourceTerm  = Assert.IsType<Button>(fenetre.FindName("BoutonSourceTerminal"));

        Assert.Equal(Visibility.Visible, diagnostic.Visibility);
        Assert.Equal(Visibility.Visible, recalibrer.Visibility);
        Assert.Equal(Visibility.Visible, sourceTerm.Visibility);

        // Il enjambe RÉELLEMENT les deux colonnes (ColumnSpan honoré) : plus de cellule vide.
        Assert.True(diagnostic.ActualWidth > recalibrer.ActualWidth * 1.8,
                    $"Diagnostic devrait enjamber les 2 colonnes : {diagnostic.ActualWidth:F1} px " +
                    $"contre {recalibrer.ActualWidth:F1} px pour une demi-colonne");

        // La cellule laisse la place aux ≈ 107 px du libellé « Recalibrer hebdo… » à FontSize=12 :
        // c'est la mesure qui EXCLUT Columns=3 (98,7 px de cellule, 74,7 px utiles).
        Assert.True(recalibrer.ActualWidth >= 120,
                    $"libellé tronqué : la cellule ne fait que {recalibrer.ActualWidth:F1} px");
        Assert.True(sourceTerm.ActualWidth > 0);
    }
}
