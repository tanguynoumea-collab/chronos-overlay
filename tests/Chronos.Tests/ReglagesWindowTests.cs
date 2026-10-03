using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.ViewModels.Historique;
using Chronos.Views.Reglages;
using Xunit;
using static Chronos.Tests.MontageReglages;

namespace Chronos.Tests;

/// <summary>
/// Quick 260927-reglages-v2 — la fenêtre de réglages refondue (DESIGN_PLAN_REGLAGES, maquettes R1 à R4), construite en STA sans
/// <c>Application</c> : chrome d'une fenêtre classique (et non plus d'un popover), mise en page mesurée à 640 × 440, 860 × 580 et
/// 1 400 × 900 (rien de coupé, rien qui chevauche), rail et raccourcis, aperçus vivants, panneau Diagnostic, et l'INVENTAIRE
/// d'iso-fonctionnalité : chaque commande de l'ancienne fenêtre est liée dans la nouvelle.
/// </summary>
[Collection("XAML WPF")]
public class ReglagesWindowTests
{
    private static T Nomme<T>(ReglagesWindow f, string nom) where T : class
        => Assert.IsAssignableFrom<T>(f.FindName(nom));

    private static double Token(ReglagesWindow f, string cle) => Assert.IsType<double>(f.FindResource(cle));

    /// <summary>Fenêtre montrable sans rien montrer à l'utilisateur : hors écran, non activée, hors barre des tâches.</summary>
    private static void PreparerHorsEcran(ReglagesWindow f)
    {
        f.WindowStartupLocation = WindowStartupLocation.Manual;
        f.Left = -20000;
        f.Top = -20000;
        f.ShowActivated = false;
        f.ShowInTaskbar = false;
    }

    // ================================================================== Chrome

    [WpfFact]
    public void La_fenetre_est_une_fenetre_classique_redimensionnable()
    {
        var f = new ReglagesWindow(NouveauVm());

        Assert.Equal(WindowStyle.None, f.WindowStyle);
        Assert.False(f.AllowsTransparency);
        Assert.False(f.Topmost);
        Assert.True(f.ShowInTaskbar);
        Assert.Equal(ResizeMode.CanResizeWithGrip, f.ResizeMode);
        Assert.Null(f.Owner);
        Assert.Contains("Réglages", f.Title);

        // Taille par défaut et minimale DEPUIS LES TOKENS (et les tokens valent ce que dit le plan).
        Assert.Equal(Token(f, "ReglagesLargeurDefaut"), f.Width);
        Assert.Equal(Token(f, "ReglagesHauteurDefaut"), f.Height);
        Assert.Equal(Token(f, "ReglagesLargeurMin"), f.MinWidth);
        Assert.Equal(Token(f, "ReglagesHauteurMin"), f.MinHeight);
        Assert.Equal((860d, 580d, 640d, 440d), (f.Width, f.Height, f.MinWidth, f.MinHeight));

        // WindowChrome : bords de redimensionnement, barre de titre native (glisser, double-clic = agrandir, accrochage).
        var chrome = WindowChrome.GetWindowChrome(f);
        Assert.NotNull(chrome);
        Assert.True(chrome.ResizeBorderThickness.Left > 0 && chrome.ResizeBorderThickness.Bottom > 0);
        Assert.Equal(Token(f, "ReglagesHauteurBarreTitre"), chrome.CaptionHeight);

        foreach (var nom in new[] { "BoutonReduire", "BoutonAgrandir", "BoutonFermer" })
            Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(Nomme<Button>(f, nom)), $"{nom} doit rester cliquable dans la barre de titre");
    }

    [WpfFact]
    public void La_desactivation_ne_ferme_plus_la_fenetre_et_Echap_la_ferme()
    {
        var f = new ReglagesWindow(NouveauVm());
        PreparerHorsEcran(f);
        var fermee = false;
        f.Closed += (_, _) => fermee = true;
        try
        {
            f.Show();

            // Un clic ailleurs (désactivation) : l'ancien popover se refermait ici.
            typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(f, new object[] { EventArgs.Empty });
            MontageReglages.Purger(Racine(f));
            Assert.False(fermee, "la fenêtre de réglages ne doit plus se fermer quand elle perd le focus");
            Assert.True(f.IsVisible);

            var echap = Assert.Single(f.InputBindings.OfType<KeyBinding>(), k => k.Key == Key.Escape && k.Modifiers == ModifierKeys.None);
            Assert.True(echap.Command.CanExecute(echap.CommandParameter));
            echap.Command.Execute(echap.CommandParameter);
            Assert.True(fermee, "Échap doit fermer la fenêtre");
        }
        finally
        {
            if (!fermee) f.Close();
        }
    }

    [WpfFact]
    public void Les_boutons_de_la_barre_de_titre_reduisent_agrandissent_restaurent_et_ferment()
    {
        var f = new ReglagesWindow(NouveauVm());   // jamais montrée : seul l'état change, rien ne s'affiche
        var clic = new RoutedEventArgs(ButtonBase.ClickEvent);

        Nomme<Button>(f, "BoutonAgrandir").RaiseEvent(clic);
        Assert.Equal(System.Windows.WindowState.Maximized, f.WindowState);
        Nomme<Button>(f, "BoutonAgrandir").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(System.Windows.WindowState.Normal, f.WindowState);
        Nomme<Button>(f, "BoutonReduire").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Assert.Equal(System.Windows.WindowState.Minimized, f.WindowState);

        var g = new ReglagesWindow(NouveauVm());
        PreparerHorsEcran(g);
        var fermee = false;
        g.Closed += (_, _) => fermee = true;
        g.Show();
        Nomme<Button>(g, "BoutonFermer").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        if (!fermee) g.Close();
        Assert.True(fermee, "✕ doit fermer la fenêtre");
    }

    [WpfFact]
    public void La_geometrie_memorisee_est_restauree_bornee_et_ecrite_en_Normal_seulement()
    {
        var x0 = SystemParameters.VirtualScreenLeft + 40;
        var y0 = SystemParameters.VirtualScreenTop + 30;
        var vm = NouveauVm(s => s with { ReglagesX = x0, ReglagesY = y0, ReglagesWidth = 700, ReglagesHeight = 500 });
        var f = new ReglagesWindow(vm);
        Assert.Equal((x0, y0, 700d, 500d), (f.Left, f.Top, f.Width, f.Height));

        // Trop petite (écrite par une ancienne version, ou éditée à la main) : ramenée au minimum 640 × 440.
        var petite = new ReglagesWindow(NouveauVm(s => s with { ReglagesX = x0, ReglagesY = y0, ReglagesWidth = 100, ReglagesHeight = 90 }));
        Assert.Equal((640d, 440d), (petite.Width, petite.Height));

        // Hors de tout écran (moniteur débranché) : ramenée dans l'écran virtuel.
        var perdue = new ReglagesWindow(NouveauVm(s => s with { ReglagesX = -100000, ReglagesY = -100000, ReglagesWidth = 700, ReglagesHeight = 500 }));
        Assert.True(perdue.Left >= SystemParameters.VirtualScreenLeft && perdue.Top >= SystemParameters.VirtualScreenTop);

        // Écriture : en Normal seulement (une fenêtre agrandie n'écrase pas la géométrie « normale » mémorisée).
        f.Left = x0 + 10;
        f.EnregistrerGeometrie();
        Assert.Equal(x0 + 10, vm.Reglages.GeometriePersistee().X);
        f.WindowState = System.Windows.WindowState.Maximized;
        f.Left = x0 + 99;
        f.EnregistrerGeometrie();
        Assert.Equal(x0 + 10, vm.Reglages.GeometriePersistee().X);
    }

    // ================================================================== Mise en page mesurée

    public static IEnumerable<object[]> TaillesEtSections()
    {
        foreach (var (l, h) in new[] { (640d, 440d), (860d, 580d), (1400d, 900d) })
            foreach (var s in Enum.GetValues<SectionReglages>())
                yield return new object[] { l, h, s };
    }

    /// <summary>
    /// Pour chaque taille (minimale, défaut, grande) et chaque section : la section est affichée, AUCUN texte, bouton ou carte
    /// n'est rogné par la mise en page (<see cref="LayoutInformation.GetLayoutClip"/> nul), aucun enfant d'un panneau empilé ne
    /// chevauche son voisin, et le rail garde ses six entrées et « Quitter Chronos » entiers dans sa hauteur.
    /// </summary>
    [WpfTheory]
    [MemberData(nameof(TaillesEtSections))]
    public void Rien_n_est_coupe_ni_ne_chevauche(double largeur, double hauteur, SectionReglages section)
    {
        var clock = new FakeClock(Now);
        var (historique, _) = NouvelHistorique(clock);
        var vm = NouveauVm(s => s with { SessionStyle = SessionStyle.Sonar }, journal: new FakeEtatJournal { DerniereEcriture = Now, RelevesEcrits = 1 },
                           clock: clock, historique: historique, ouvreur: new FakeOuvreurHistorique(),
                           sessions: new FakeSessionsController { Enabled = true });
        var taille = new Size(largeur, hauteur);
        var f = Monter(vm, section, taille);
        var racine = Racine(f);

        var panneau = Assert.IsAssignableFrom<FrameworkElement>(f.FindName("Section" + section));
        Assert.True(EstAffiche(panneau, racine), $"la section {section} doit être affichée");

        var coupes = new List<string>();
        foreach (var e in Descendants(racine).OfType<FrameworkElement>())
        {
            if (e is not (TextBlock or ButtonBase or Border)) continue;
            if (!EstAffiche(e, racine) || e.ActualWidth <= 0) continue;
            if (AncetreDeType<TextBoxBase>(e) || AncetreDeType<ScrollBar>(e) || AncetreDeType<Viewbox>(e) || AncetreDeType<ResizeGrip>(e)) continue;
            if (LayoutInformation.GetLayoutClip(e) is not null)
                coupes.Add($"{e.GetType().Name} « {Libelle(e)} » ({e.ActualWidth:F0} × {e.ActualHeight:F0})");
        }
        Assert.True(coupes.Count == 0, $"{largeur} × {hauteur}, {section} : éléments rognés :\n  " + string.Join("\n  ", coupes));

        var chevauchements = new List<string>();
        foreach (var p in Descendants(racine).OfType<Panel>().Where(p => p is StackPanel or WrapPanel && EstAffiche(p, racine)))
        {
            var enfants = p.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible && c.ActualWidth > 0 && c.ActualHeight > 0)
                           .Select(c => (c, r: c.TransformToAncestor(p).TransformBounds(new Rect(c.RenderSize)))).ToList();
            for (var i = 0; i < enfants.Count; i++)
                for (var j = i + 1; j < enfants.Count; j++)
                {
                    var inter = Rect.Intersect(enfants[i].r, enfants[j].r);
                    if (!inter.IsEmpty && inter.Width > 0.5 && inter.Height > 0.5)
                        chevauchements.Add($"{Libelle(enfants[i].c)} ∩ {Libelle(enfants[j].c)}");
                }
        }
        Assert.True(chevauchements.Count == 0, $"{largeur} × {hauteur}, {section} : chevauchements :\n  " + string.Join("\n  ", chevauchements));

        // Le rail ne rétrécit jamais : largeur fixe, six entrées et « Quitter » entiers dans sa hauteur.
        var rail = Nomme<FrameworkElement>(f, "Rail");
        Assert.Equal(Token(f, "ReglagesRailLargeur"), rail.ActualWidth, 1);
        var liste = Nomme<ListBox>(f, "ListeSections");
        var elements = Enumerable.Range(0, 6).Select(i => Assert.IsType<ListBoxItem>(liste.ItemContainerGenerator.ContainerFromIndex(i)))
                                 .Cast<FrameworkElement>().Append(Nomme<Button>(f, "BoutonQuitter")).ToList();
        foreach (var e in elements)
        {
            var r = e.TransformToAncestor(rail).TransformBounds(new Rect(e.RenderSize));
            Assert.True(r.Top >= 0 && r.Bottom <= rail.ActualHeight + 0.5, $"{Libelle(e)} déborde du rail ({r.Top:F0} → {r.Bottom:F0} sur {rail.ActualHeight:F0})");
        }
    }

    [WpfTheory]
    [InlineData(640d, 440d, 3)]
    [InlineData(860d, 580d, 6)]   // 858 − 208 (rail) − 8 (défilement) − 48 (marges) = 594 px → 6 × 96
    public void Les_vignettes_de_theme_passent_a_la_ligne_entieres(double largeur, double hauteur, int parLigneAttendu)
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Apparence, new Size(largeur, hauteur));
        var defilement = Nomme<ScrollViewer>(f, "DefilementContenu");
        var grille = Nomme<ItemsControl>(f, "GrilleThemes");

        // Les vignettes vivent dans les ItemsControl internes des groupes : collecte par l'arbre, ordre visuel (haut, gauche).
        var vignetteStyle = (Style)f.FindResource("VignetteTheme");
        var placees = Descendants(grille).OfType<Button>().Where(b => b.Style == vignetteStyle)
            .Select(v => (v, r: v.TransformToAncestor(defilement).TransformBounds(new Rect(v.RenderSize))))
            .OrderBy(x => Math.Round(x.r.Top)).ThenBy(x => x.r.Left)
            .ToList();
        var vignettes = placees.Select(x => x.v).ToList();
        var rects = placees.Select(x => x.r).ToList();
        Assert.Equal(ThemeCatalog.All.Count, vignettes.Count);
        Assert.Equal(15, vignettes.Count);

        foreach (var (v, r) in vignettes.Zip(rects))
        {
            Assert.Equal(Token(f, "ReglagesVignetteLargeur"), v.ActualWidth, 1);
            Assert.Equal(Token(f, "ReglagesVignetteHauteur"), v.ActualHeight, 1);
            Assert.True(r.Left >= 0 && r.Right <= defilement.ViewportWidth + 0.5, $"vignette coupée à droite : {r.Right:F0} > {defilement.ViewportWidth:F0}");
        }

        var premiereLigne = rects.Count(r => Math.Abs(r.Top - rects[0].Top) < 1);
        Assert.Equal(parLigneAttendu, premiereLigne);
    }

    [WpfFact]
    public void Les_themes_sont_ranges_en_trois_groupes_titres()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Apparence, ParDefaut);
        var defilement = Nomme<ScrollViewer>(f, "DefilementContenu");
        var grille = Nomme<ItemsControl>(f, "GrilleThemes");
        var etiquette = (Style)f.FindResource("Etiquette");

        var titres = Descendants(grille).OfType<TextBlock>().Where(t => t.DataContext is GroupeThemes && t.Style == etiquette)
            .OrderBy(t => t.TransformToAncestor(defilement).Transform(new Point(0, 0)).Y)
            .ToList();

        Assert.Equal(new[] { "PÂLE", "CLASSIQUE", "VIVE" }, titres.Select(t => t.Text).ToArray());
        foreach (var t in titres)
        {
            Assert.Equal(Token(f, "ReglagesCorpsEtiquette"), t.FontSize, 3);
            Assert.Equal(FontWeights.SemiBold, t.FontWeight);
        }
    }

    [WpfFact]
    public void Une_vignette_de_chaque_groupe_selectionne_son_theme()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Apparence, ParDefaut);
        var grille = Nomme<ItemsControl>(f, "GrilleThemes");
        var vignetteStyle = (Style)f.FindResource("VignetteTheme");
        var vignettes = Descendants(grille).OfType<Button>().Where(b => b.Style == vignetteStyle).ToList();

        // Une par groupe : la liaison doit atteindre SelectThemeCommand depuis l'ItemsControl imbriqué (Pitfall 3).
        foreach (var nom in new[] { "Moka", "Graphite", "Synthwave" })
        {
            var bouton = Assert.Single(vignettes, b => b.DataContext is ThemeChoice c && c.Name == nom);
            var choix = (ThemeChoice)bouton.DataContext;
            Assert.NotNull(bouton.Command);
            bouton.Command.Execute(bouton.CommandParameter);
            Assert.Equal(choix.Theme.Key, vm.SelectedThemeKey);
            Assert.True(choix.IsSelected);
            Assert.Single(vm.Themes, t => t.IsSelected);
        }
    }

    // ================================================================== Rail, raccourcis, Quitter

    [WpfFact]
    public void Le_rail_et_Ctrl_1_a_6_pilotent_la_section()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Donnees);
        var liste = Nomme<ListBox>(f, "ListeSections");

        Assert.Equal(0, liste.SelectedIndex);
        liste.SelectedIndex = 4;   // ce que font ↑ / ↓ et le clic dans une ListBox
        Assert.Equal(SectionReglages.Comportement, vm.Reglages.Section);

        vm.Reglages.AllerACommand.Execute(SectionReglages.Apparence);
        MettreEnPage(f, ParDefaut);
        Assert.Equal(2, liste.SelectedIndex);

        foreach (var (touches, n) in Enumerable.Range(1, 6).SelectMany(n => new[] { (Key.D0 + n, n), (Key.NumPad0 + n, n) }))
        {
            var kb = Assert.Single(f.InputBindings.OfType<KeyBinding>(), k => k.Key == touches && k.Modifiers == ModifierKeys.Control);
            Assert.Same(vm.Reglages.AllerAuNumeroCommand, kb.Command);
            kb.Command.Execute(kb.CommandParameter);
            Assert.Equal(vm.Reglages.Entrees[n - 1].Section, vm.Reglages.Section);
        }

        // Focus visible : l'entrée du rail réagit au focus clavier (et pas seulement à la souris).
        Assert.Contains(liste.ItemContainerStyle.Triggers.OfType<Trigger>(), t => t.Property == UIElement.IsKeyboardFocusedProperty);
    }

    [WpfFact]
    public void Quitter_est_isole_en_bas_du_rail_en_brosse_Danger()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Donnees);
        var quitter = Nomme<Button>(f, "BoutonQuitter");
        var liste = Nomme<ListBox>(f, "ListeSections");
        var rail = Nomme<FrameworkElement>(f, "Rail");

        Assert.Same(vm.QuitCommand, quitter.Command);
        Assert.Equal(((SolidColorBrush)f.FindResource("Danger")).Color, Assert.IsType<SolidColorBrush>(quitter.Foreground).Color);
        Assert.False(AncetreDeType<ListBox>(quitter), "« Quitter » ne doit pas être une entrée de navigation");
        var basListe = liste.TransformToAncestor(rail).TransformBounds(new Rect(liste.RenderSize)).Bottom;
        var hautQuitter = quitter.TransformToAncestor(rail).TransformBounds(new Rect(quitter.RenderSize)).Top;
        Assert.True(hautQuitter > basListe, "« Quitter » doit être sous la liste des sections, séparé");
        Assert.Contains(Descendants(quitter).OfType<TextBlock>(), t => t.Text == "Quitter Chronos");
    }

    // ================================================================== Iso-fonctionnalité (DESIGN_PLAN §4)

    /// <summary>
    /// L'inventaire complet : chaque commande de l'ancienne fenêtre (et le diagnostic, et Quitter) est LIÉE dans la nouvelle, dans
    /// la section que dit le plan. Une commande supprimée, ou liée à la mauvaise instance, compile sans bruit : ce test la voit.
    /// </summary>
    [WpfFact]
    public void Chaque_commande_de_l_ancienne_fenetre_est_liee_dans_la_nouvelle()
    {
        var clock = new FakeClock(Now);
        var (historique, _) = NouvelHistorique(clock);
        var vm = NouveauVm(s => s with { SessionStyle = SessionStyle.Sonar }, journal: new FakeEtatJournal { DerniereEcriture = Now, RelevesEcrits = 1 },
                           clock: clock, historique: historique, ouvreur: new FakeOuvreurHistorique(),
                           sessions: new FakeSessionsController { Enabled = true });
        var f = Monter(vm, SectionReglages.Donnees);

        var attendu = new (string Nom, SectionReglages Section, ICommand Commande)[]
        {
            ("LoginClaude", SectionReglages.Donnees, vm.LoginClaudeCommand),
            ("ToggleSondeEnTetes", SectionReglages.Donnees, vm.ToggleSondeEnTetesCommand),
            ("OuvrirHistorique", SectionReglages.Historique, vm.OuvrirHistoriqueCommand),
            ("SelectTheme", SectionReglages.Apparence, vm.SelectThemeCommand),
            ("SelectCadranStyle", SectionReglages.Apparence, vm.SelectCadranStyleCommand),
            ("ToggleCadranMode", SectionReglages.Apparence, vm.ToggleCadranModeCommand),
            ("ToggleSessionsWidget", SectionReglages.Sessions, vm.ToggleSessionsWidgetCommand),
            ("SelectSessionStyle", SectionReglages.Sessions, vm.SelectSessionStyleCommand),
            ("ToggleVerticalLayout", SectionReglages.Sessions, vm.ToggleVerticalLayoutCommand),
            ("ToggleBackground", SectionReglages.Comportement, vm.ToggleBackgroundCommand),
            ("ToggleAutostart", SectionReglages.Comportement, vm.ToggleAutostartCommand),
            ("Reglages.ActualiserDiagnostic", SectionReglages.Diagnostic, vm.Reglages.ActualiserDiagnosticCommand),
            ("Reglages.CopierDiagnostic", SectionReglages.Diagnostic, vm.Reglages.CopierDiagnosticCommand),
        };

        var manquantes = new List<string>();
        foreach (var groupe in attendu.GroupBy(a => a.Section))
        {
            vm.Reglages.AllerACommand.Execute(groupe.Key);
            MettreEnPage(f, ParDefaut);
            var liees = Descendants(Racine(f)).OfType<ICommandSource>()
                .Where(c => c is DependencyObject d && EstAffiche(d, Racine(f)))
                .Select(c => c.Command).Where(c => c is not null).ToList();
            foreach (var a in groupe)
                if (!liees.Any(c => ReferenceEquals(c, a.Commande))) manquantes.Add($"{a.Nom} (section {a.Section})");

            // Quitter est lié sur toutes les sections (bas du rail).
            if (!liees.Any(c => ReferenceEquals(c, vm.QuitCommand))) manquantes.Add($"Quit (visible en section {groupe.Key})");
        }

        Assert.True(manquantes.Count == 0, "commandes de l'ancienne fenêtre absentes de la nouvelle :\n  " + string.Join("\n  ", manquantes));
    }

    [WpfFact]
    public void Les_libelles_du_plan_sont_la_et_Source_terminal_est_renomme()
    {
        var clock = new FakeClock(Now);
        var (historique, _) = NouvelHistorique(clock);
        var vm = NouveauVm(s => s with { SessionStyle = SessionStyle.Sonar }, clock: clock, historique: historique,
                           ouvreur: new FakeOuvreurHistorique(), sessions: new FakeSessionsController { Enabled = true });
        var f = Monter(vm, SectionReglages.Donnees);

        var textes = new HashSet<string>();
        foreach (var s in Enum.GetValues<SectionReglages>())
        {
            vm.Reglages.AllerACommand.Execute(s);
            MettreEnPage(f, ParDefaut);
            foreach (var t in Descendants(Racine(f)).OfType<TextBlock>().Where(t => EstAffiche(t, Racine(f))))
                textes.Add(t.Text ?? "");
            foreach (var b in Descendants(Racine(f)).OfType<ContentControl>().Where(b => EstAffiche(b, Racine(f))))
                if (b.Content is string c) textes.Add(c);
        }

        foreach (var libelle in new[]
                 {
                     "Données", "Historique", "Apparence", "Sessions", "Comportement", "Diagnostic",
                     "Connexion Claude", "Sonde d'en-têtes",
                     "Historique d'utilisation", "THÈME", "STYLE DU CADRAN", "Mode étendu",
                     "Widget sessions Claude Code", "STYLE DU WIDGET", "Disposition verticale",
                     "Arrière-plan", "Lancer au démarrage",
                     "↻ Actualiser", "⧉ Copier", "Quitter Chronos",
                 })
            Assert.True(textes.Contains(libelle), $"libellé du plan absent : « {libelle} »");

        Assert.DoesNotContain(textes, t => t.Contains("Source terminal"));
        Assert.DoesNotContain(textes, t => t.Contains("Barre de statut de Claude Code"));   // DAT-03 (37-05) : carte retirée
        Assert.DoesNotContain(textes, t => t.Contains("Recalibrer"));   // DAT-02 (37-04) : carte retirée
        Assert.DoesNotContain("RÉGLAGES", textes);
    }

    [WpfFact]
    public void La_barre_de_statut_a_quitte_Donnees()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Donnees);
        Assert.Null(f.FindName("InterrupteurBarreStatut"));   // DAT-03 (37-05) : la barre est retirée, plus d'interrupteur
    }

    [WpfFact]
    public void Le_recalibrage_hebdo_a_quitte_Comportement()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Comportement);
        Assert.Null(f.FindName("BoutonRecalibrerHebdo"));
    }

    // ================================================================== Apparence : aperçu vivant

    [WpfFact]
    public void L_apercu_montre_le_vrai_cadran_et_sa_legende_suit_theme_et_style()
    {
        var vm = NouveauVm();
        var cadran = new Grid();   // tient lieu du contenu de MainWindow, déjà mis en page à l'empreinte de Fusible H (190 × 66)
        cadran.UseLayoutRounding = false;   // dimensions exactes en DIP, indépendantes du DPI de la machine de test (125 % : 110 → 110,4)
        cadran.Measure(new Size(190, 66));
        cadran.Arrange(new Rect(0, 0, 190, 66));
        var f = Monter(vm, SectionReglages.Apparence, ParDefaut, cadran);

        var apercu = Nomme<Shape>(f, "ApercuCadran");
        var pinceau = Assert.IsType<VisualBrush>(apercu.Fill);
        Assert.Same(cadran, pinceau.Visual);
        Assert.True(EstAffiche(apercu, Racine(f)));
        // Cadré sur l'empreinte du cadran, pas sur la boîte englobante de son dessin (qui bouge avec les pastilles).
        Assert.Equal(BrushMappingMode.Absolute, pinceau.ViewboxUnits);
        Assert.Equal(new Rect(0, 0, 190, 66), pinceau.Viewbox);
        // Phase 40 : l'empreinte change (style ou orientation) → le pinceau recadre sur la nouvelle empreinte réelle.
        cadran.Measure(new Size(110, 190));
        cadran.Arrange(new Rect(0, 0, 110, 190));
        cadran.UpdateLayout();
        Assert.Equal(new Rect(0, 0, 110, 190), pinceau.Viewbox);
        Assert.Equal(Visibility.Collapsed, Nomme<FrameworkElement>(f, "ApercuCadranAbsent").Visibility);

        Assert.Equal("Thème : Minuit", Nomme<TextBlock>(f, "LegendeTheme").Text);
        Assert.Equal("Style : Anneaux", Nomme<TextBlock>(f, "LegendeStyle").Text);
        Assert.Equal(Visibility.Visible, Nomme<FrameworkElement>(f, "CarteModeEtendu").Visibility);

        vm.SelectThemeCommand.Execute(vm.Themes.Single(t => t.Theme.Key == "aurore"));
        vm.SelectCadranStyleCommand.Execute(vm.CadranStyles.Single(c => c.Style == CadranStyle.Braises));
        MettreEnPage(f, ParDefaut);

        Assert.Equal("Thème : Aurore", Nomme<TextBlock>(f, "LegendeTheme").Text);
        Assert.Equal("Style : Braises", Nomme<TextBlock>(f, "LegendeStyle").Text);
        Assert.Equal(Visibility.Collapsed, Nomme<FrameworkElement>(f, "CarteModeEtendu").Visibility);   // Anneaux seulement
    }

    /// <summary>§11 B1 : la fenêtre des cadrans rectangulaires porte, sous l'empreinte, la bande de 14 px des pastilles. L'aperçu
    /// des réglages reste cadré sur l'EMPREINTE du cadran (bande exclue) ; Arcs (bande 0) garde tout son carré.</summary>
    [WpfFact]
    public void L_apercu_exclut_la_bande_des_pastilles()
    {
        var vm = NouveauVm();
        vm.CadranStyle = CadranStyle.Fusible;   // horizontal par défaut : empreinte 190 × 92, fenêtre 190 × 106
        var cadran = new Grid { UseLayoutRounding = false };
        cadran.Measure(new Size(190, 106));
        cadran.Arrange(new Rect(0, 0, 190, 106));
        var f = Monter(vm, SectionReglages.Apparence, ParDefaut, cadran);

        var pinceau = Assert.IsType<VisualBrush>(Nomme<Shape>(f, "ApercuCadran").Fill);
        Assert.Equal(new Rect(0, 0, 190, 92), pinceau.Viewbox);

        vm.CadranStyle = CadranStyle.Arcs;
        cadran.Measure(new Size(170, 170));
        cadran.Arrange(new Rect(0, 0, 170, 170));
        cadran.UpdateLayout();
        Assert.Equal(new Rect(0, 0, 170, 170), pinceau.Viewbox);
    }

    // ================================================================== Orientation (phase 40, CAD-04)

    private static Border BordurePuce(Button b)
    {
        b.ApplyTemplate();
        return Assert.IsType<Border>(b.Template.FindName("puce", b));
    }

    [WpfFact]
    public void La_carte_orientation_est_visible_pour_les_cadrans_rectangulaires()
    {
        var vm = NouveauVm(s => s with { CadranStyle = CadranStyle.Fusible });
        var f = Monter(vm, SectionReglages.Apparence);

        Assert.True(EstAffiche(Nomme<FrameworkElement>(f, "CarteOrientation"), Racine(f)));
        Assert.Equal(Visibility.Collapsed, Nomme<FrameworkElement>(f, "CarteModeEtendu").Visibility);

        var h = Nomme<Button>(f, "BoutonOrientationHorizontale");
        var v = Nomme<Button>(f, "BoutonOrientationVerticale");
        Assert.Same(vm.ChoisirOrientationCommand, h.Command);
        Assert.Same(vm.ChoisirOrientationCommand, v.Command);
        Assert.Equal(OrientationCadran.Horizontal, h.CommandParameter);
        Assert.Equal(OrientationCadran.Vertical, v.CommandParameter);
        Assert.Equal(true, h.Tag);
        Assert.Equal(false, v.Tag);
    }

    [WpfFact]
    public void Cliquer_vertical_change_l_orientation_du_cadran_courant()
    {
        var vm = NouveauVm(s => s with { CadranStyle = CadranStyle.Fusible });
        var f = Monter(vm, SectionReglages.Apparence);
        var v = Nomme<Button>(f, "BoutonOrientationVerticale");

        v.Command.Execute(v.CommandParameter);
        MettreEnPage(f, ParDefaut);

        Assert.Equal(Orientation.Vertical, vm.OrientationFusible);
        Assert.Equal(true, v.Tag);
        Assert.Equal(false, Nomme<Button>(f, "BoutonOrientationHorizontale").Tag);
        Assert.Same(f.FindResource("Accent"), BordurePuce(v).BorderBrush);
    }

    [WpfFact]
    public void La_carte_orientation_est_masquee_pour_Arcs_et_Braises()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Apparence);
        Assert.Equal(Visibility.Collapsed, Nomme<FrameworkElement>(f, "CarteOrientation").Visibility);

        vm.SelectCadranStyleCommand.Execute(vm.CadranStyles.Single(c => c.Style == CadranStyle.Braises));
        MettreEnPage(f, ParDefaut);
        Assert.Equal(Visibility.Collapsed, Nomme<FrameworkElement>(f, "CarteOrientation").Visibility);

        vm.SelectCadranStyleCommand.Execute(vm.CadranStyles.Single(c => c.Style == CadranStyle.Maree));
        MettreEnPage(f, ParDefaut);
        Assert.True(EstAffiche(Nomme<FrameworkElement>(f, "CarteOrientation"), Racine(f)));
    }

    // ================================================================== Sessions

    [WpfFact]
    public void Sessions_widget_desactive_grise_les_styles_et_invite_puis_montre_l_apercu()
    {
        var sessions = new FakeSessionsController { Enabled = false };
        var vm = NouveauVm(sessions: sessions);
        var f = Monter(vm, SectionReglages.Sessions);
        var racine = Racine(f);

        Assert.False(EstAffiche(Nomme<FrameworkElement>(f, "CarteApercuSessions"), racine));
        var bloc = Nomme<FrameworkElement>(f, "BlocStylesWidget");
        Assert.True(EstAffiche(bloc, racine));
        Assert.False(bloc.IsEnabled);
        Assert.Equal(Token(f, "ReglagesOpaciteInactif"), bloc.Opacity);
        var invitation = Nomme<FrameworkElement>(f, "InvitationWidget");
        Assert.True(EstAffiche(invitation, racine));
        Assert.Contains(Descendants(invitation).OfType<TextBlock>(), t => t.Text == "Active le widget pour choisir son style.");

        vm.ToggleSessionsWidgetCommand.Execute(null);
        MettreEnPage(f, ParDefaut);

        Assert.True(EstAffiche(Nomme<FrameworkElement>(f, "CarteApercuSessions"), racine));
        Assert.True(bloc.IsEnabled);
        Assert.Equal(1d, bloc.Opacity);
        Assert.False(EstAffiche(invitation, racine));

        // L'aperçu vivant suit le style choisi (mêmes gabarits que le widget réel).
        var apercu = Nomme<ContentControl>(f, "ApercuSessions");
        Assert.Same(vm.Reglages.ApercuSessions, apercu.Content);
        vm.SelectSessionStyleCommand.Execute(vm.SessionStyles.Single(s => s.Style == SessionStyle.Marge));
        MettreEnPage(f, ParDefaut);
        Assert.Same(apercu.FindResource("TplMarge"), apercu.ContentTemplate);
    }

    // ================================================================== Diagnostic (remplace la boîte de message)

    [WpfFact]
    public async Task Le_diagnostic_s_affiche_dans_la_fenetre_se_copie_et_s_etire()
    {
        var presse = new FakePressePapiers();
        var vm = NouveauVm(pressePapiers: presse);
        var f = Monter(vm, SectionReglages.Diagnostic);
        await vm.Reglages.ActualiserDiagnosticCommand.ExecutionTask!;   // lancée à l'entrée dans la section, sur le pool
        MettreEnPage(f, ParDefaut);

        var rapport = Nomme<TextBox>(f, "RapportDiagnostic");
        Assert.Equal(EtatDiagnostic.Pret, vm.Reglages.EtatDiagnostic);
        Assert.True(EstAffiche(rapport, Racine(f)));
        Assert.True(rapport.IsReadOnly);
        Assert.Contains("Diagnostic", rapport.Text);
        Assert.Equal(vm.Reglages.TexteDiagnostic, rapport.Text);
        Assert.Matches("Consolas|Cascadia", rapport.FontFamily.Source);   // police mono
        Assert.Equal(ScrollBarVisibility.Auto, rapport.VerticalScrollBarVisibility);
        Assert.Matches(@"^Généré à \d\d:\d\d · \d+ lignes?$", Nomme<TextBlock>(f, "LigneDiagnostic").Text);

        Assert.Same(vm.Reglages.ActualiserDiagnosticCommand, Nomme<Button>(f, "BoutonActualiserDiagnostic").Command);
        var copier = Nomme<Button>(f, "BoutonCopierDiagnostic");
        Assert.Same(vm.Reglages.CopierDiagnosticCommand, copier.Command);
        copier.Command!.Execute(null);
        Assert.Equal(rapport.Text, presse.Dernier);

        // Il s'étire avec la fenêtre (il occupe toute la hauteur disponible).
        var h1 = rapport.ActualHeight;
        MettreEnPage(f, Grande);
        Assert.True(rapport.ActualHeight > h1 + 200, $"le rapport doit s'étirer : {h1:F0} puis {rapport.ActualHeight:F0}");
    }

    [WpfFact]
    public void Generation_et_echec_du_diagnostic_s_ecrivent_dans_la_fenetre()
    {
        var vm = NouveauVm();
        var f = Monter(vm, SectionReglages.Donnees);
        vm.Reglages.AllerACommand.Execute(SectionReglages.Diagnostic);

        vm.Reglages.EtatDiagnostic = EtatDiagnostic.EnCours;
        MettreEnPage(f, ParDefaut);
        var generation = Nomme<TextBlock>(f, "TexteGenerationDiagnostic");
        Assert.True(EstAffiche(generation, Racine(f)));
        Assert.Equal(ReglagesViewModel.TexteGeneration, generation.Text);

        vm.Reglages.ErreurDiagnostic = ReglagesViewModel.PrefixeEchec + "coffre illisible";
        vm.Reglages.EtatDiagnostic = EtatDiagnostic.Echec;
        MettreEnPage(f, ParDefaut);
        Assert.False(EstAffiche(generation, Racine(f)));
        var echec = Nomme<FrameworkElement>(f, "PanneauEchecDiagnostic");
        Assert.True(EstAffiche(echec, Racine(f)));
        Assert.Contains(Descendants(echec).OfType<TextBlock>(), t => t.Text == "Le diagnostic a échoué : coffre illisible");
        Assert.Same(vm.Reglages.ActualiserDiagnosticCommand, Nomme<Button>(f, "BoutonReessayerDiagnostic").Command);
        Assert.False(EstAffiche(Nomme<TextBox>(f, "RapportDiagnostic"), Racine(f)));
    }

    // ================================================================== Aides

    private static (HistoriqueViewModel vm, ReglagesHistoriqueMemoire reglages) NouvelHistorique(FakeClock clock)
    {
        var tz = BornesPlage.FuseauParisPourTests();
        var reglages = new ReglagesHistoriqueMemoire();
        return (new HistoriqueViewModel(new FakeSourceHistorique(tz), new FakeUiDispatcher { OnUiThread = true }, clock, tz, reglages), reglages);
    }

    private static bool AncetreDeType<T>(DependencyObject e) where T : DependencyObject
    {
        for (var d = VisualTreeHelper.GetParent(e); d is not null; d = VisualTreeHelper.GetParent(d))
            if (d is T) return true;
        return false;
    }

    private static string Libelle(FrameworkElement e) => e switch
    {
        TextBlock t => t.Text,
        ContentControl { Content: string s } => s,
        _ => string.IsNullOrEmpty(e.Name) ? e.GetType().Name : e.Name,
    };
}
