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

    // Source substituée : rend une liste FIXE couvrant les CINQ états, pour qu'aucun template ne soit
    // mesuré à vide (une fenêtre sans élément passerait le test sans rien prouver). Le cinquième —
    // l'attente DÉDUITE d'EVT-04 — porte le libellé le PLUS LONG des cinq (quatorze caractères) : sans
    // lui dans la liste, la matrice 8 styles × 9 thèmes ne mesurerait rien du cas le plus contraignant.
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
            new SessionSnapshot("s4", "legacy",        SessionActivity.Unknown,          null, Maintenant.AddMinutes(-12)),
            new SessionSnapshot("s5", "interrompu",    SessionActivity.WaitingDeduced,   null, Maintenant.AddMinutes(-25)));

        var monitor = new SessionMonitor(TempDir(), source, new ArchiveStore(Path.Combine(TempDir(), "a.json")));
        var vm = new SessionsViewModel(monitor, new FakeClock(Maintenant), new ArchiveStore(Path.Combine(TempDir(), "b.json")),
                                       new TreatedStore(Path.Combine(TempDir(), "t.json"), new FakeClock(Maintenant)));
        vm.Refresh(Maintenant);
        Assert.Equal(5, vm.Items.Count);   // une fenêtre vide ne prouverait rien
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

    // Parcourt l'arbre VISUEL et rend les menus contextuels attachés. Un ContextMenu n'est PAS dans
    // l'arbre visuel de la fenêtre (il a le sien, créé à l'ouverture) : il est la VALEUR d'une propriété,
    // et c'est là qu'on va le chercher — ce qui permet de le vérifier sans jamais ouvrir de menu, donc
    // sans afficher quoi que ce soit.
    private static IEnumerable<ContextMenu> MenusContextuels(DependencyObject racine)
    {
        var n = VisualTreeHelper.GetChildrenCount(racine);
        for (var i = 0; i < n; i++)
        {
            var enfant = VisualTreeHelper.GetChild(racine, i);
            if (enfant is FrameworkElement fe && fe.ContextMenu is { } menu) yield return menu;
            foreach (var petit in MenusContextuels(enfant)) yield return petit;
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

        // 5 sessions × 1 séparateur (état · détail). Deux par session = un orphelin laissé par le retrait
        // du libellé de type ; zéro = on a supprimé le mauvais TextBlock.
        Assert.Equal(5, separateurs);
    }

    /// <summary>
    /// TRT-03, versant ÉCRAN. La garde de SOURCE compte le câblage dans le texte du XAML ; celle-ci monte
    /// les 72 combinaisons et exige que CHAQUE menu trouvé porte bien ses trois entrées. Aucun menu n'est
    /// ouvert et aucune fenêtre n'est affichée : un <c>ContextMenu</c> est la VALEUR d'une propriété,
    /// peuplée par le BAML au chargement.
    /// </summary>
    [WpfFact]
    public void Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes()
    {
        var styles = Enum.GetValues<SessionStyle>();
        var themes = ThemeCatalog.All;
        Assert.Equal(8, styles.Length);
        Assert.Equal(9, themes.Count);

        var stylesCouverts = new HashSet<SessionStyle>();

        foreach (var theme in themes)
        foreach (var style in styles)
        {
            var vm = Vm();
            vm.Style = style;
            vm.SetTheme(theme);
            var (_, racine) = Monter(vm, theme);

            var menus = MenusContextuels(racine).ToList();
            Assert.True(menus.Count >= 1,
                $"aucun menu contextuel pour le style {style} et le thème {theme.Key} : le geste explicite "
                + "n'y est pas offert");
            foreach (var menu in menus)
                Assert.Equal(3, menu.Items.OfType<MenuItem>().Count());
            stylesCouverts.Add(style);
        }

        Assert.Equal(8, stylesCouverts.Count);   // la matrice n'a pas été traversée à vide
    }

    /// <summary>
    /// TRT-03, versant ORDRE et TEXTE — la lacune que la vérification de la phase 26 a MESURÉE
    /// (avertissement n° 1) : le geste définitif déplacé en tête du menu et les deux libellés statiques
    /// réduits à « Archiver » / « Marquer traitée » laissaient la suite entièrement verte, parce que les
    /// deux gardes existantes ne font que COMPTER — nombre de <c>MenuItem</c>, nombre de séparateurs,
    /// nombre d'occurrences de chaque commande. Un compte est indifférent à l'ordre et au texte.
    ///
    /// <para>Ce que ce cas exige, sur les HUIT gabarits : le geste RÉVERSIBLE unitaire et le geste de
    /// masse AVANT le séparateur, le geste DÉFINITIF strictement APRÈS lui, et chacun des trois libellés
    /// annonçant si la session revient — « revient si elle me redemande », « ne revient jamais », et
    /// « reviennent » pour le libellé calculé par le ViewModel. Le verrou établi dans ce projet
    /// (<c>LoginClaudeCommand</c> bascule, et un clic effacerait le coffre de jetons) s'applique mot pour
    /// mot : une entrée destructive mise en tête d'un menu se clique par réflexe.</para>
    ///
    /// <para>Aucun menu n'est ouvert et aucune fenêtre n'est affichée. Un <c>ContextMenu</c> est la VALEUR
    /// d'une propriété, peuplée par le BAML. Son <c>DataContext</c> est lui-même bindé sur
    /// <c>PlacementTarget.DataContext</c>, qui reste NUL tant que le menu n'a pas été ouvert : on vérifie
    /// d'abord que ce binding est bien là, puis on pose la ligne à la main — exactement ce que
    /// l'ouverture ferait — pour que les libellés et les commandes s'évaluent réellement.</para>
    /// </summary>
    [WpfFact]
    public void Dans_les_huit_menus_le_reversible_precede_le_destructif_et_les_libelles_le_disent()
    {
        var styles = Enum.GetValues<SessionStyle>();
        Assert.Equal(8, styles.Length);

        var stylesCouverts = new HashSet<SessionStyle>();
        var menusInspectes = 0;

        foreach (var style in styles)
        {
            var vm = Vm();
            vm.Style = style;
            vm.SetTheme(ThemeCatalog.Default);
            var (_, racine) = Monter(vm, ThemeCatalog.Default);

            var ligne = vm.Items[0];
            var menus = MenusContextuels(racine).ToList();
            Assert.True(menus.Count >= 1, $"aucun menu contextuel pour le style {style}");

            foreach (var menu in menus)
            {
                var liaison = System.Windows.Data.BindingOperations.GetBinding(menu, FrameworkElement.DataContextProperty);
                Assert.True(liaison is not null,
                    $"style {style} : le menu n'est plus rattaché à sa ligne — sans DataContext bindé, "
                    + "les trois entrées se lieraient à autre chose que la session cliquée");
                Assert.Equal("PlacementTarget.DataContext", liaison!.Path.Path);

                // Ce que l'ouverture ferait, sans ouvrir : le menu n'est jamais affiché.
                menu.DataContext = ligne;
                menu.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                var entrees = menu.Items.Cast<object>().ToList();

                var iSeparateur = entrees.FindIndex(o => o is Separator);
                var iTraitee = entrees.FindIndex(o => o is MenuItem m && ReferenceEquals(m.Command, ligne.MarquerTraiteeCommand));
                var iToutTraiter = entrees.FindIndex(o => o is MenuItem m && ReferenceEquals(m.Command, ligne.MarquerToutTraiteCommand));
                var iArchiver = entrees.FindIndex(o => o is MenuItem m && ReferenceEquals(m.Command, ligne.ArchiveCommand));

                Assert.True(iSeparateur >= 0, $"style {style} : plus de séparateur dans le menu");
                Assert.True(iTraitee >= 0, $"style {style} : le geste réversible unitaire n'est plus lié");
                Assert.True(iToutTraiter >= 0, $"style {style} : le geste de masse n'est plus lié");
                Assert.True(iArchiver >= 0, $"style {style} : le geste définitif n'est plus lié");

                // L'ORDRE : les deux gestes qui se reprennent en tête, le geste sans retour en queue,
                // derrière le trait qui les sépare.
                Assert.True(iTraitee < iSeparateur,
                    $"style {style} : « Marquer traitée » (index {iTraitee}) doit précéder le séparateur "
                    + $"(index {iSeparateur})");
                Assert.True(iToutTraiter < iSeparateur,
                    $"style {style} : le geste de masse (index {iToutTraiter}) doit précéder le séparateur "
                    + $"(index {iSeparateur})");
                Assert.True(iSeparateur < iArchiver,
                    $"style {style} : le séparateur (index {iSeparateur}) doit précéder « Archiver "
                    + $"définitivement » (index {iArchiver}) — un geste DÉFINITIF ne se met pas en tête de menu");

                // LE TEXTE : chacun dit si la session revient. Lu sur le Header réellement rendu.
                var texteTraitee = Assert.IsType<string>(((MenuItem)entrees[iTraitee]).Header);
                var texteToutTraiter = Assert.IsType<string>(((MenuItem)entrees[iToutTraiter]).Header);
                var texteArchiver = Assert.IsType<string>(((MenuItem)entrees[iArchiver]).Header);

                Assert.Contains("revient si elle me redemande", texteTraitee, StringComparison.Ordinal);
                Assert.DoesNotContain("ne revient jamais", texteTraitee, StringComparison.Ordinal);

                Assert.Contains("reviennent", texteToutTraiter, StringComparison.Ordinal);

                Assert.Contains("ne revient jamais", texteArchiver, StringComparison.Ordinal);
                Assert.DoesNotContain("revient si elle me redemande", texteArchiver, StringComparison.Ordinal);

                menusInspectes++;
            }

            stylesCouverts.Add(style);
        }

        Assert.Equal(8, stylesCouverts.Count);            // les huit gabarits, pas un de moins
        Assert.True(menusInspectes >= 8, $"seulement {menusInspectes} menus inspectés");
    }
}
