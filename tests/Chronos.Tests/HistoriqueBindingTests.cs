using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using Chronos.Controls.Historique;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.ViewModels.Historique;
using Chronos.Views.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-01 — smoke test BAML de la COQUILLE de la fenêtre Historique (34-05) : chrome de consultation (WindowChrome, sans
/// transparence, sans Owner, sans Topmost), tailles lues dans les tokens, Échap → <c>FermerCommand</c>, en-tête §2.1 complet
/// (« 4 semaines » désactivé avec son infobulle), segment et style actifs visibles, bandeau F2 qui vit et disparaît, pastille
/// « journal muet », géométrie restaurée bornée puis réécrite en <c>Normal</c> seulement, deux vues hébergées et remplies, pinceaux du
/// thème actif visibles DANS les vues (34-08 : le dictionnaire fusionné par chaque vue ne doit pas ombrer le thème).
///
/// Ce que <c>dotnet build</c> ne voit pas : un bouton bindé sur la mauvaise commande, une infobulle absente sur un bouton
/// désactivé (clic inerte), un <c>Topmost</c> ou un <c>AllowsTransparency</c> revenus par copier-coller de <c>SettingsWindow</c>.
/// Rituel de <c>ReglagesBindingTests.MonterReglages</c> ; collection sérialisée obligatoire (course du chargeur BAML).
/// Aucun fichier : réglages en mémoire, source de démonstration, horloge figée.
/// </summary>
[Collection("XAML WPF")]
public class HistoriqueBindingTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static readonly DateTimeOffset Now = ScenariosHistorique.Maintenant(Tz);

    private sealed record Banc(HistoriqueWindow Fenetre, HistoriqueViewModel Vm, FrameworkElement Racine, ReglagesHistoriqueMemoire Reglages);

    /// <summary>Construit la fenêtre sur le scénario de référence et met en page sa racine (920 × 610).</summary>
    private static Banc Monter(FakeEtatReconstruction? recon = null, FakeEtatJournal? journal = null,
                               ReglagesHistoriqueMemoire? reglages = null, IClock? clock = null)
    {
        reglages ??= new ReglagesHistoriqueMemoire();
        var vm = new HistoriqueViewModel(new SourceHistoriqueDemonstration(Tz), new FakeUiDispatcher { OnUiThread = true },
                                         clock ?? ScenariosHistorique.HorlogeFigee(Tz), Tz, reglages, journal, recon);
        vm.Ouvrir();
        vm.AttendreLecture().GetAwaiter().GetResult();

        var fenetre = new HistoriqueWindow(vm);
        var racine = (FrameworkElement)fenetre.Content!;
        racine.DataContext = vm;
        Idle(racine);
        racine.Measure(new Size(920, 610));
        racine.Arrange(new Rect(0, 0, 920, 610));
        return new Banc(fenetre, vm, racine, reglages);
    }

    private static void Idle(DispatcherObject o) => o.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static IEnumerable<T> Tous<T>(DependencyObject racine) where T : DependencyObject
    {
        if (racine is T t) yield return t;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var x in Tous<T>(VisualTreeHelper.GetChild(racine, i)))
                yield return x;
    }

    private static IEnumerable<TextBlock> TousLesTextBlocks(DependencyObject racine)
        => Tous<TextBlock>(racine).Where(tb => tb.IsVisible || tb.Visibility == Visibility.Visible);

    private static Button Bouton(DependencyObject racine, string contenu)
        => Assert.Single(Tous<Button>(racine), b => b.Content as string == contenu);

    private static Color CouleurDe(Brush? b) => Assert.IsType<SolidColorBrush>(b).Color;

    // ------------------------------------------------------------------ Chrome

    [WpfFact]
    public void La_fenetre_est_une_fenetre_de_consultation_pas_un_overlay()
    {
        var (fenetre, _, _, _) = Monter();

        Assert.Equal(WindowStyle.None, fenetre.WindowStyle);
        Assert.False(fenetre.AllowsTransparency, "AllowsTransparency=True ferait une fenêtre layered (coût de composition), DESIGN_PLAN §2");
        Assert.False(fenetre.Topmost, "une fenêtre de consultation n'est pas un overlay");
        Assert.True(fenetre.ShowInTaskbar);
        Assert.Equal(ResizeMode.CanResize, fenetre.ResizeMode);
        Assert.Null(fenetre.Owner);
        Assert.Equal("Chronos — Historique", fenetre.Title);

        var chrome = WindowChrome.GetWindowChrome(fenetre);
        Assert.NotNull(chrome);
        Assert.Equal(0, chrome!.CaptionHeight);
        Assert.Equal(6, chrome.ResizeBorderThickness.Left);
        Assert.Equal(new Thickness(0), chrome.GlassFrameThickness);
        Assert.False(chrome.UseAeroCaptionButtons);

        Assert.Equal(Color.FromRgb(0x15, 0x13, 0x22), CouleurDe(fenetre.Background));   // Panel : les coins « carrés » ont la couleur du fond

        // Coins DWM best-effort (D-34-22) : un handle nul (ou Windows 10) échoue EN SILENCE — jamais d'exception.
        Assert.False(HistoriqueWindow.ArrondirCoinsDwm(IntPtr.Zero));
    }

    [WpfFact]
    public void Les_tailles_minimale_et_par_defaut_viennent_des_tokens()
    {
        var (fenetre, _, _, _) = Monter();

        Assert.Equal(760, fenetre.MinWidth);
        Assert.Equal(480, fenetre.MinHeight);
        Assert.Equal(920, fenetre.Width);
        Assert.Equal(610, fenetre.Height);
        Assert.Equal(WindowStartupLocation.CenterScreen, fenetre.WindowStartupLocation);
    }

    [WpfFact]
    public void Echap_est_lie_a_la_commande_de_fermeture_et_la_commande_ferme()
    {
        var (fenetre, vm, _, _) = Monter();

        var echap = Assert.Single(fenetre.InputBindings.OfType<KeyBinding>(), k => k.Key == Key.Escape);
        Assert.Same(vm.FermerCommand, echap.Command);

        // La fenêtre n'a jamais été montrée : le code-behind compte la demande et n'appelle Close() que si IsLoaded.
        vm.FermerCommand.Execute(null);
        Assert.Equal(1, fenetre.DemandesDeFermeture);
    }

    // ------------------------------------------------------------------ En-tête §2.1

    [WpfFact]
    public void L_en_tete_commun_est_complet()
    {
        var (_, vm, racine, _) = Monter();
        var textes = TousLesTextBlocks(racine).Select(t => t.Text).ToList();

        foreach (var attendu in new[] { "Historique", "Jour", "Semaine", "4 semaines", vm.LibellePeriode, vm.TexteFraicheur,
                                        "Cette semaine", "Style :", "Pistes", "Simplifié", "Tuiles", "‹", "›" })
            Assert.Contains(attendu, textes);
        Assert.NotEqual("", vm.LibellePeriode);
        Assert.NotEqual("", vm.TexteFraicheur);

        var quatre = Bouton(racine, "4 semaines");
        Assert.False(quatre.IsEnabled, "« 4 semaines » n'existe pas avant la phase 35 : désactivé, jamais un clic inerte");
        Assert.Equal("bientôt (phase 35)", quatre.ToolTip);
        Assert.True(ToolTipService.GetShowOnDisabled(quatre), "l'infobulle doit s'afficher sur le bouton désactivé");

        var semaine = Bouton(racine, "Semaine");
        Assert.Same(vm.ChoisirVueCommand, semaine.Command);
        Assert.Equal(VueHistorique.Semaine, semaine.CommandParameter);
        var jour = Bouton(racine, "Jour");
        Assert.Same(vm.ChoisirVueCommand, jour.Command);
        Assert.Equal(VueHistorique.Jour, jour.CommandParameter);

        Assert.Same(vm.PrecedentCommand, Bouton(racine, "‹").Command);
        Assert.Same(vm.SuivantCommand, Bouton(racine, "›").Command);
        Assert.Same(vm.RetourPresentCommand, Bouton(racine, "Cette semaine").Command);
        Assert.Same(vm.FermerCommand, Bouton(racine, "✕").Command);

        Assert.Same(vm.ChoisirStyleCommand, Bouton(racine, "Tuiles").Command);
        Assert.Equal(HistoriqueStyleSemaine.Tuiles, Bouton(racine, "Tuiles").CommandParameter);
    }

    [WpfFact]
    public void Le_segment_actif_et_le_style_actif_se_voient()
    {
        var (fenetre, vm, racine, _) = Monter();
        var accent = Color.FromRgb(0x8B, 0x7B, 0xF0);

        var semaine = Bouton(racine, "Semaine");
        Assert.Equal(accent, CouleurDe(semaine.BorderBrush));
        Assert.Equal(1.5, semaine.BorderThickness.Left);
        Assert.Equal(Colors.Transparent, CouleurDe(Bouton(racine, "Jour").BorderBrush));

        Assert.Equal(accent, CouleurDe(Bouton(racine, "Pistes").BorderBrush));
        vm.ChoisirStyleCommand.Execute(HistoriqueStyleSemaine.Tuiles);
        Idle(racine);
        Assert.Equal(accent, CouleurDe(Bouton(racine, "Tuiles").BorderBrush));
        Assert.Equal(Colors.Transparent, CouleurDe(Bouton(racine, "Pistes").BorderBrush));

        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        Idle(racine);
        var selecteur = Assert.IsAssignableFrom<FrameworkElement>(fenetre.FindName("SelecteurStyle"));
        Assert.Equal(Visibility.Collapsed, selecteur.Visibility);
        Assert.Equal(accent, CouleurDe(Bouton(racine, "Jour").BorderBrush));
        Assert.Equal(Colors.Transparent, CouleurDe(semaine.BorderBrush));
        Assert.Same(vm.RetourPresentCommand, Bouton(racine, "Aujourd'hui").Command);
    }

    // ------------------------------------------------------------------ Bandeau F2

    [WpfFact]
    public void Le_bandeau_F2_est_visible_en_reconstruction_et_disparait_ensuite()
    {
        var recon = new FakeEtatReconstruction { Phase = PhaseReconstruction.Reconstruction, FichiersTraites = 886, FichiersTotal = 1603, SemaineCouranteDisponible = true };
        var (fenetre, _, racine, _) = Monter(recon: recon);

        var bandeau = Assert.IsAssignableFrom<FrameworkElement>(fenetre.FindName("BandeauF2"));
        Assert.Equal(Visibility.Visible, bandeau.Visibility);
        var textes = TousLesTextBlocks(bandeau).Select(t => t.Text).ToList();
        Assert.Contains("Reconstruction des tokens depuis vos transcripts Claude Code — 886 / 1603 fichiers · la semaine courante est déjà complète", textes);
        Assert.Contains(textes, t => t.EndsWith("ils commencent au 14 sept. 2026", StringComparison.Ordinal));

        var barre = Assert.IsType<ProgressBar>(fenetre.FindName("BarreF2"));
        Assert.Equal(1, barre.Maximum);
        Assert.Equal(886.0 / 1603, barre.Value, 9);

        recon.Phase = PhaseReconstruction.Incremental;
        recon.Declencher();
        Idle(racine);
        Assert.Equal(Visibility.Collapsed, bandeau.Visibility);

        var (sans, _, _, _) = Monter();
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)sans.FindName("BandeauF2")).Visibility);
    }

    // ------------------------------------------------------------------ Pastille « journal muet »

    [WpfFact]
    public void La_pastille_d_alerte_suit_le_journal_muet()
    {
        // Horloge démarrée 30 min avant « maintenant » : l'alerte compte depuis max(démarrage, dernière écriture) (D-32-21).
        var journal = new FakeEtatJournal { DerniereEcriture = Now - TimeSpan.FromMinutes(16) };
        var (fenetre, vm, racine, _) = Monter(journal: journal, clock: new FakeClock(Now - TimeSpan.FromMinutes(30)));

        vm.Tick(Now);
        Idle(racine);
        var pastille = Assert.IsAssignableFrom<FrameworkElement>(fenetre.FindName("PastilleAlerte"));
        Assert.Equal(Visibility.Visible, pastille.Visibility);
        Assert.Contains("journal muet depuis 16 min", TousLesTextBlocks(pastille).Select(t => t.Text));
        var point = Assert.Single(Tous<Ellipse>(pastille));
        Assert.Equal(CouleurDe(vm.Theme.BrushTokens()["Alerte"]), CouleurDe(point.Fill));

        journal.DerniereEcriture = Now - TimeSpan.FromMinutes(1);
        vm.Tick(Now);
        Idle(racine);
        Assert.Equal(Visibility.Collapsed, pastille.Visibility);
    }

    // ------------------------------------------------------------------ Géométrie persistée

    [WpfFact]
    public void La_geometrie_persistee_est_restauree_bornee_et_reecrite()
    {
        var reglages = new ReglagesHistoriqueMemoire(new ChronosSettings { HistoriqueX = 100, HistoriqueY = 50, HistoriqueWidth = 900, HistoriqueHeight = 600 });
        var (fenetre, _, _, _) = Monter(reglages: reglages);

        Assert.Equal(WindowStartupLocation.Manual, fenetre.WindowStartupLocation);
        Assert.Equal(100, fenetre.Left);
        Assert.Equal(50, fenetre.Top);
        Assert.Equal(900, fenetre.Width);
        Assert.Equal(600, fenetre.Height);

        var horsEcran = new ReglagesHistoriqueMemoire(new ChronosSettings { HistoriqueX = 99999, HistoriqueY = 50, HistoriqueWidth = 900, HistoriqueHeight = 600 });
        var (loin, _, _, _) = Monter(reglages: horsEcran);
        Assert.True(loin.Left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 900,
                    $"Left={loin.Left} doit être ramené dans l'écran virtuel (Pitfall 2)");

        fenetre.Left = 120;
        fenetre.Top = 60;
        fenetre.EnregistrerGeometrie();
        Assert.Equal(120, reglages.Courant.HistoriqueX);
        Assert.Equal(60, reglages.Courant.HistoriqueY);
        Assert.Equal(900, reglages.Courant.HistoriqueWidth);
        Assert.Equal(600, reglages.Courant.HistoriqueHeight);

        // Maximisée : la géométrie « Normal » mémorisée ne doit pas être écrasée par celle de l'écran entier.
        fenetre.WindowState = WindowState.Maximized;
        fenetre.Left = 0;
        fenetre.Top = 0;
        fenetre.EnregistrerGeometrie();
        Assert.Equal(120, reglages.Courant.HistoriqueX);
        Assert.Equal(60, reglages.Courant.HistoriqueY);
        Assert.Equal(900, reglages.Courant.HistoriqueWidth);
        Assert.Equal(600, reglages.Courant.HistoriqueHeight);
    }

    // ------------------------------------------------------------------ Vues hébergées (remplies par 34-06 / 34-07)

    [WpfFact]
    public void Les_deux_vues_sont_hebergees_et_remplies()
    {
        var (fenetre, vm, racine, _) = Monter();

        var semaine = Assert.Single(Tous<VueSemaineView>(racine));
        var jour = Assert.Single(Tous<VueJourView>(racine));
        Assert.Equal(Visibility.Visible, semaine.Visibility);
        Assert.Equal(Visibility.Collapsed, jour.Visibility);

        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        Idle(racine);
        Assert.Equal(Visibility.Collapsed, semaine.Visibility);
        Assert.Equal(Visibility.Visible, jour.Visibility);

        Assert.NotEmpty(Assert.IsType<Grid>(semaine.Content).Children);   // 34-06 : la vue Semaine est remplie (VueSemaineBindingTests)
        Assert.NotEmpty(Assert.IsType<Grid>(jour.Content).Children);   // 34-07 : la vue Jour est remplie (VueJourBindingTests)
        Assert.Same(vm, semaine.DataContext);
        Assert.Same(vm, jour.DataContext);

        Assert.True(fenetre.Resources.Contains("Alerte"), "les pinceaux du thème doivent être injectés dans les ressources de la fenêtre");
        Assert.Contains(fenetre.Resources.MergedDictionaries, d => d.Source?.OriginalString.Contains("DesignTokens.xaml", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// 34-08 (écart 5 de 34-07) — chaque vue fusionne <c>DesignTokens.xaml</c> pour se monter seule ; ce dictionnaire porte le repli
    /// STATIQUE <c>Alerte</c> (#EFA23A, l'ambre de Minuit). Un <c>{DynamicResource Alerte}</c> posé DANS une vue le trouve avant le
    /// pinceau du thème injecté dans la fenêtre : avec un thème autre que Minuit, l'ambre des trous « jeton » resterait celui de Minuit.
    /// Thème « Nord » (ambre #EBCB8B) : le mot, la bordure du trou et la bande de couverture doivent porter l'ambre DU THÈME, en Semaine
    /// comme en Jour.
    /// </summary>
    [WpfFact]
    public void Les_vues_hebergees_suivent_les_pinceaux_du_theme_actif()
    {
        var reglages = new ReglagesHistoriqueMemoire(new ChronosSettings { ThemeKey = "nord" });
        var (_, vm, racine, _) = Monter(reglages: reglages);
        var ambre = CouleurDe(vm.Theme.BrushTokens()["Alerte"]);
        Assert.Equal("nord", vm.Theme.Key);
        Assert.NotEqual(Color.FromRgb(0xEF, 0xA2, 0x3A), ambre);   // sinon le test ne distingue rien

        var semaine = Assert.Single(Tous<VueSemaineView>(racine));
        Assert.Equal(ambre, CouleurDe(Assert.Single(Tous<TextBlock>(semaine), t => t.Text == "jeton invalide" && VisibleDans(t, semaine)).Foreground));
        Assert.All(Tous<PisteNiveau>(semaine), p => Assert.Equal(ambre, CouleurDe(p.BordTrouJeton)));
        Assert.All(Tous<PisteCouverture>(semaine), p => Assert.Equal(ambre, CouleurDe(p.Jeton)));

        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        vm.AttendreLecture().GetAwaiter().GetResult();
        Idle(racine);
        racine.Measure(new Size(920, 610));
        racine.Arrange(new Rect(0, 0, 920, 610));
        racine.UpdateLayout();
        var jour = Assert.Single(Tous<VueJourView>(racine));
        Assert.Equal(ambre, CouleurDe(Assert.Single(Tous<TextBlock>(jour), t => t.Text == "jeton invalide").Foreground));
        Assert.Equal(ambre, CouleurDe(Assert.Single(Tous<PisteNiveau>(jour)).BordTrouJeton));
        Assert.Equal(ambre, CouleurDe(Assert.Single(Tous<PisteCouverture>(jour)).Jeton));
    }

    /// <summary>Vrai si aucun ancêtre de <paramref name="e"/> jusqu'à <paramref name="racine"/> n'est caché (la grille de style active).</summary>
    private static bool VisibleDans(DependencyObject e, DependencyObject racine)
    {
        for (var x = e; x is not null && !ReferenceEquals(x, racine); x = VisualTreeHelper.GetParent(x))
            if (x is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }
}
