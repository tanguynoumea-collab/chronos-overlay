using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Critère de succès 4 de la phase 21 : « aucun trou visuel ». Le retrait du libellé de type (bindé dans
/// le seul template Pastilles, l. 70-79 de SessionStyles.xaml) ne doit laisser ni case vide ni décalage,
/// sur les 8 styles de session et les 9 thèmes — 72 combinaisons.
///
/// SUBSTITUTION ASSUMÉE : la contrainte de phase interdit de lancer l'overlay (une instance est en cours
/// d'utilisation). Ce test est la vérification NON DESTRUCTIVE qui remplace le contrôle à l'œil : il charge
/// le vrai BAML, applique les vrais pinceaux du thème et fait faire à WPF sa passe de mise en page.
///
/// Aucun accès au vrai %APPDATA%\Chronos : le moniteur pointe des dossiers temporaires et sa source de
/// base est substituée.
/// </summary>
[Collection("XAML WPF")]   // le chargeur BAML n'est pas sûr en accès concurrent — voir XamlWpfCollection
public class SessionStylesBindingTests
{
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-styles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);
        return d;
    }

    // Source substituée : rend une liste FIXE couvrant les QUATRE états, pour qu'aucun template ne soit
    // mesuré à vide (une fenêtre sans élément passerait le test sans rien prouver).
    private sealed class SourceFixe : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _snaps;
        public SourceFixe(params SessionSnapshot[] snaps) => _snaps = snaps;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _snaps;
    }

    private static SessionsViewModel Vm()
    {
        var source = new SourceFixe(
            new SessionSnapshot("s1", "overlay",       SessionActivity.WaitingAttention, "permission_prompt", Maintenant),
            new SessionSnapshot("s2", "api-migration", SessionActivity.WaitingTurn,      null, Maintenant.AddMinutes(-3)),
            new SessionSnapshot("s3", "chronos",       SessionActivity.Working,          null, Maintenant),
            new SessionSnapshot("s4", "legacy",        SessionActivity.Unknown,          null, Maintenant.AddMinutes(-12)));

        var monitor = new SessionMonitor(TempDir(), source, new ArchiveStore(Path.Combine(TempDir(), "a.json")));
        var vm = new SessionsViewModel(monitor, new FakeClock(Maintenant), new ArchiveStore(Path.Combine(TempDir(), "b.json")));
        vm.Refresh(Maintenant);
        Assert.Equal(4, vm.Items.Count);   // une fenêtre vide ne prouverait rien
        return vm;
    }

    /// <summary>
    /// Monte la fenêtre et met en page sa GRILLE RACINE, selon le motif documenté de
    /// <see cref="CadranBindingTests"/> : une fenêtre jamais affichée n'a PAS de template appliqué, donc
    /// son <c>Content</c> n'a aucun parent visuel — <c>fenetre.Measure()</c> rend alors 0×0 et aucun
    /// binding ne s'évalue. Poser le DataContext sur la grille rétablit une évaluation RÉELLE ; purger la
    /// file du Dispatcher évite qu'une réévaluation différée laisse l'arbre à son état par défaut.
    /// </summary>
    private static (SessionsWindow fenetre, FrameworkElement racine) Monter(SessionsViewModel vm, ChronosTheme theme)
    {
        var fenetre = new SessionsWindow(vm);
        fenetre.ApplyThemeBrushes(theme);

        var racine = (FrameworkElement)fenetre.Content!;
        racine.DataContext = vm;
        racine.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        racine.Measure(new Size(400, 400));
        racine.Arrange(new Rect(0, 0, 400, 400));
        racine.UpdateLayout();
        return (fenetre, racine);
    }

    // Parcourt l'arbre VISUEL et rend les TextBlock effectivement visibles.
    private static IEnumerable<TextBlock> TextesVisibles(DependencyObject racine)
    {
        var n = VisualTreeHelper.GetChildrenCount(racine);
        for (var i = 0; i < n; i++)
        {
            var enfant = VisualTreeHelper.GetChild(racine, i);
            if (enfant is TextBlock tb && tb.Visibility == Visibility.Visible) yield return tb;
            foreach (var petit in TextesVisibles(enfant)) yield return petit;
        }
    }

    [WpfFact]
    public void Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent()
    {
        var styles = Enum.GetValues<SessionStyle>();
        var themes = ThemeCatalog.All;

        Assert.Equal(8, styles.Length);      // garde anti-muette : la matrice est bien 8 × 9
        Assert.Equal(9, themes.Count);

        foreach (var theme in themes)
        foreach (var style in styles)
        {
            var vm = Vm();
            vm.Style = style;
            vm.SetTheme(theme);

            var (_, racine) = Monter(vm, theme);

            Assert.True(racine.DesiredSize.Width > 0 && racine.DesiredSize.Height > 0,
                $"taille dégénérée pour le style {style} et le thème {theme.Key} : {racine.DesiredSize}");
        }
    }

    [WpfFact]
    public void Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session()
    {
        var vm = Vm();
        vm.Style = SessionStyle.Pastilles;
        vm.SetTheme(ThemeCatalog.Default);

        var (_, racine) = Monter(vm, ThemeCatalog.Default);

        var separateurs = TextesVisibles(racine).Count(tb => tb.Text is "  ·  ");

        // 4 sessions × 1 séparateur (état · détail). Deux par session = un orphelin laissé par le retrait
        // du libellé de type ; zéro = on a supprimé le mauvais TextBlock.
        Assert.Equal(4, separateurs);
    }
}
