using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Chronos.Models;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.ViewModels;
using Chronos.ViewModels.Historique;
using Chronos.Views.Reglages;
using Xunit;
using WindowState = Chronos.Models.WindowState; // lève l'ambiguïté avec System.Windows.WindowState

namespace Chronos.Tests;

/// <summary>
/// Smoke test BAML de la fenêtre de RÉGLAGES (HDR-06, HDR-03/HDR-04, JRN-04, ACC-01) — porté sur la fenêtre refondue du quick
/// 260927-reglages-v2 (<see cref="ReglagesWindow"/>) sans perdre une assertion de fond : chaque test monte la fenêtre sur la
/// SECTION où le plan range désormais le réglage (Données pour la sonde, Historique pour la carte F1).
///
/// Ce que ces tests attrapent et que <c>dotnet build</c> ne voit pas :
/// <list type="bullet">
///   <item>l'interrupteur de la sonde bindé sur la MAUVAISE commande — compile parfaitement, et un clic
///         supprimerait le coffre de jetons (<c>LoginClaude</c>) au lieu de couper la seule source qui
///         dépense ;</item>
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
    private static readonly DateTimeOffset Now = MontageReglages.Now;

    /// <summary>
    /// Monte la fenêtre de réglages dans l'état voulu, sur la section voulue, et met en page sa racine (voir
    /// <see cref="MontageReglages"/> : DataContext posé sur la racine du contenu, purge du Dispatcher, Measure/Arrange — sans quoi
    /// les bindings ne s'évaluent pas et les tests seraient verts pour de mauvaises raisons).
    /// </summary>
    private static (ReglagesWindow fenetre, MainViewModel vm) MonterReglages(
        bool sondeActivee, StatutServeur? statutCinqHeures = null,
        FakeEtatJournal? journal = null, FakeClock? clock = null,
        HistoriqueViewModel? historique = null, IOuvreurHistorique? ouvreur = null,
        SectionReglages section = SectionReglages.Donnees)
    {
        var vm = MontageReglages.NouveauVm(s => s with { SondeEnTetesActivee = sondeActivee },
                                           journal: journal, clock: clock ?? new FakeClock(Now), historique: historique, ouvreur: ouvreur);

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

        return (MontageReglages.Monter(vm, section), vm);
    }

    /// <summary>Parcourt l'arbre VISUEL (seul peuplé après Arrange) et rend tous les TextBlock.</summary>
    private static IEnumerable<TextBlock> TousLesTextBlocks(DependencyObject racine)
        => MontageReglages.Descendants(racine).OfType<TextBlock>();

    /// <summary>
    /// HDR-06 — LE piège du plan, verrouillé au niveau du XAML et non du seul ViewModel : l'interrupteur de
    /// la sonde ne doit pas pouvoir être confondu au câblage. Bindé sur <c>LoginClaudeCommand</c>, un clic
    /// supprimerait le coffre de jetons en laissant la sonde dépenser.
    /// </summary>
    [WpfFact]
    public void L_interrupteur_de_sonde_est_binde_sur_SA_commande_et_non_sur_une_autre()
    {
        var (fenetre, vm) = MonterReglages(sondeActivee: true);
        var interrupteur = Assert.IsType<ToggleButton>(fenetre.FindName("InterrupteurSonde"));

        Assert.Same(vm.ToggleSondeEnTetesCommand, interrupteur.Command);
        Assert.NotSame(vm.LoginClaudeCommand, interrupteur.Command);
    }

    /// <summary>HDR-06 — l'interrupteur est un MIROIR de l'état persisté, dans les deux sens. Bindé sur
    /// une autre propriété booléenne (par exemple IsSessionsWidgetEnabled ou IsLoggedIn), l'une des deux
    /// branches tomberait.</summary>
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
    /// prélève sans le dire. Si quelqu'un retire le libellé ou son chiffre, ce test tombe. Il doit être
    /// AFFICHÉ dans la section Données, pas seulement présent dans l'arbre.
    /// </summary>
    [WpfFact]
    public void Le_cout_de_la_sonde_est_ECRIT_dans_les_reglages()
    {
        var (fenetre, _) = MonterReglages(sondeActivee: true);
        var racine = MontageReglages.Racine(fenetre);

        var libelle = TousLesTextBlocks(racine)
            .Where(t => MontageReglages.EstAffiche(t, racine))
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

    // --- JRN-04 (phase 32, 32-05) : ligne d'état du journal — dernière écriture, pastille Alerte ---

    private static UsageSnapshot SnapshotSimple() => new()
    {
        FiveHour = new WindowState
        {
            Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.5,
            ResetsAt = Now + TimeSpan.FromHours(2),
        },
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        SourceCapturedAt = Now,
    };

    /// <summary>Purge la file du Dispatcher : les réévaluations de binding déclenchées par un changement de propriété
    /// après le montage peuvent être différées (même raison qu'au montage).</summary>
    private static void Purger(ReglagesWindow fenetre) => MontageReglages.Purger(MontageReglages.Racine(fenetre));

    /// <summary>
    /// JRN-04 — la ligne d'état du journal est LIÉE au ViewModel (même texte que <c>TexteEtatJournal</c>), et ce texte porte
    /// l'âge de la dernière écriture et le compte de relevés. Un XAML qui binderait une autre propriété (ou un texte figé)
    /// compilerait sans bruit : c'est ce que ce test attrape.
    /// </summary>
    [WpfFact]
    public void La_ligne_d_etat_du_journal_est_liee_au_ViewModel()
    {
        var clock = new FakeClock(Now);
        var journal = new FakeEtatJournal { RelevesEcrits = 2 };
        var (fenetre, vm) = MonterReglages(sondeActivee: false, journal: journal, clock: clock, section: SectionReglages.Historique);

        journal.DerniereEcriture = Now + TimeSpan.FromMinutes(1);
        clock.UtcNow = Now + TimeSpan.FromMinutes(5);
        vm.ApplySnapshot(SnapshotSimple());
        Purger(fenetre);

        var ligne = Assert.IsType<TextBlock>(fenetre.FindName("LigneEtatJournal"));
        Assert.Equal(vm.TexteEtatJournal, ligne.Text);
        Assert.Contains("dernière écriture il y a 4 min", ligne.Text);
        Assert.Contains("2 relevés depuis le démarrage", ligne.Text);
    }

    /// <summary>
    /// JRN-04 — la pastille <c>Alerte</c> ne se voit QU'EN alerte : 16 min sans écriture alors que Chronos tourne. Vérifié sur la
    /// <c>Visibility</c> RÉSOLUE après montage (piège du plan 17-05 : sans montage, elle resterait à son défaut Visible).
    /// </summary>
    [WpfFact]
    public void La_pastille_du_journal_ne_se_voit_qu_en_alerte()
    {
        var clock = new FakeClock(Now);
        var journal = new FakeEtatJournal { DerniereEcriture = Now, RelevesEcrits = 1 };
        var (fenetre, vm) = MonterReglages(sondeActivee: false, journal: journal, clock: clock, section: SectionReglages.Historique);

        var pastille = Assert.IsType<System.Windows.Shapes.Ellipse>(fenetre.FindName("PastilleJournal"));
        Assert.False(vm.AlerteJournal);
        Assert.Equal(Visibility.Collapsed, pastille.Visibility);

        clock.UtcNow = Now + TimeSpan.FromMinutes(16);
        vm.ApplySnapshot(SnapshotSimple());
        Purger(fenetre);

        Assert.True(vm.AlerteJournal);
        Assert.Equal(Visibility.Visible, pastille.Visibility);
        Assert.StartsWith("journal muet depuis 16 min", vm.TexteEtatJournal);
    }

    // --- ACC-01 (35-02) : carte F1 « Historique d'utilisation » — fusion avec le journal, même HistoriqueViewModel que la fenêtre ---

    /// <summary>Le VM de la fenêtre Historique tel que la DI le fournit (singleton), sur réglages en mémoire et fuseau Paris.</summary>
    private static (HistoriqueViewModel vm, ReglagesHistoriqueMemoire reglages) NouvelHistorique(FakeClock clock)
    {
        var tz = BornesPlage.FuseauParisPourTests();
        var reglages = new ReglagesHistoriqueMemoire();
        var vm = new HistoriqueViewModel(new FakeSourceHistorique(tz), new FakeUiDispatcher { OnUiThread = true }, clock, tz, reglages);
        return (vm, reglages);
    }

    /// <summary>
    /// D-35-09 conservé : la carte « Historique d'utilisation » ABSORBE la ligne d'état du journal (plus de carte « Journal des
    /// relevés »), porte sa bordure Accent 1,5 et la mention du double-clic. Réglages v2 : elle est désormais la PREMIÈRE carte de
    /// la section Historique (juste après le titre et la phrase d'aide), et non plus la troisième carte de Données.
    /// </summary>
    [WpfFact]
    public void La_carte_Historique_remplace_la_carte_du_journal()
    {
        var clock = new FakeClock(Now);
        var (historique, _) = NouvelHistorique(clock);
        var journal = new FakeEtatJournal { DerniereEcriture = Now, RelevesEcrits = 1 };
        var (fenetre, _) = MonterReglages(sondeActivee: false, journal: journal, clock: clock,
                                          historique: historique, ouvreur: new FakeOuvreurHistorique(), section: SectionReglages.Historique);
        var racine = MontageReglages.Racine(fenetre);
        var textes = TousLesTextBlocks(racine).ToList();

        var titre = Assert.Single(textes, t => t.Text == "Historique d'utilisation");
        Assert.DoesNotContain(textes, t => (t.Text ?? "").Contains("Journal des relevés"));
        Assert.DoesNotContain(textes, t => (t.Text ?? "").Contains("sans interface"));
        Assert.IsType<TextBlock>(fenetre.FindName("LigneEtatJournal"));
        Assert.IsType<System.Windows.Shapes.Ellipse>(fenetre.FindName("PastilleJournal"));

        var carte = Assert.IsType<Border>(fenetre.FindName("CarteHistorique"));
        Assert.True(MontageReglages.EstAffiche(carte, racine));
        Assert.True(MontageReglages.EstAffiche(titre, racine));
        Assert.Equal(((SolidColorBrush)fenetre.FindResource("Accent")).Color, Assert.IsType<SolidColorBrush>(carte.BorderBrush).Color);
        Assert.Equal(new Thickness(1.5), carte.BorderThickness);

        // HISTORIQUE : titre → aide → carte « Historique d'utilisation » (la première carte de la section).
        var section = Assert.IsType<StackPanel>(fenetre.FindName("SectionHistorique"));
        Assert.Equal(section, VisualTreeHelper.GetParent(carte));
        var premiereCarte = section.Children.OfType<Border>().First();
        Assert.Same(carte, premiereCarte);
        Assert.Equal("Historique", Assert.IsType<TextBlock>(section.Children[0]).Text);

        var mention = Assert.Single(textes, t => t.Text == "Aussi : double-clic au centre du cadran");
        Assert.True(MontageReglages.EstAffiche(mention, racine));

        // Et la section Données ne la montre plus : un réglage, un seul endroit.
        var donnees = Assert.IsType<StackPanel>(fenetre.FindName("SectionDonnees"));
        Assert.DoesNotContain(carte, donnees.Children.OfType<Border>());
    }

    [WpfFact]
    public void La_carte_Historique_pilote_le_meme_style_que_la_fenetre()
    {
        var clock = new FakeClock(Now);
        var (historique, reglages) = NouvelHistorique(clock);
        var (fenetre, vm) = MonterReglages(sondeActivee: false, clock: clock, historique: historique, ouvreur: new FakeOuvreurHistorique(),
                                           section: SectionReglages.Historique);

        Assert.Same(historique, vm.Historique);   // MÊME instance que la fenêtre : aucun état de style dupliqué (D-35-08)

        var tuiles = Assert.IsType<Button>(fenetre.FindName("PuceStyleTuiles"));
        Assert.Same(historique.ChoisirStyleCommand, tuiles.Command);
        Assert.Equal(HistoriqueStyleSemaine.Tuiles, tuiles.CommandParameter);
        tuiles.Command!.Execute(tuiles.CommandParameter);
        Assert.Equal(HistoriqueStyleSemaine.Tuiles, historique.Style);
        Assert.Equal(HistoriqueStyleSemaine.Tuiles, reglages.Lire().HistoriqueStyleSemaine);

        historique.ChoisirStyleCommand.Execute(HistoriqueStyleSemaine.Simplifie);   // comme le sélecteur de la fenêtre
        Purger(fenetre);

        var simplifie = Assert.IsType<Button>(fenetre.FindName("PuceStyleSimplifie"));
        Assert.Equal(true, simplifie.Tag);
        Assert.Equal(false, tuiles.Tag);
        var bordure = Assert.IsType<Border>(simplifie.Template.FindName("puce", simplifie));
        Assert.Equal(((SolidColorBrush)fenetre.FindResource("Accent")).Color, Assert.IsType<SolidColorBrush>(bordure.BorderBrush).Color);
    }

    [WpfFact]
    public void Le_bouton_Ouvrir_ouvre_la_fenetre()
    {
        var clock = new FakeClock(Now);
        var (historique, _) = NouvelHistorique(clock);
        var ouvreur = new FakeOuvreurHistorique();
        var (fenetre, vm) = MonterReglages(sondeActivee: false, clock: clock, historique: historique, ouvreur: ouvreur,
                                           section: SectionReglages.Historique);

        var bouton = Assert.IsType<Button>(fenetre.FindName("BoutonOuvrirHistorique"));
        Assert.Equal("Ouvrir", bouton.Content);
        Assert.Same(vm.OuvrirHistoriqueCommand, bouton.Command);
        Assert.True(MontageReglages.EstAffiche(bouton, MontageReglages.Racine(fenetre)));
        bouton.Command!.Execute(null);
        Assert.Equal(1, ouvreur.Ouvertures);

        // Sans Historique ni journal injectés (tests historiques) : la carte entière reste masquée.
        var (nue, _) = MonterReglages(sondeActivee: false, section: SectionReglages.Historique);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<Border>(nue.FindName("CarteHistorique")).Visibility);
    }

    [WpfFact]
    public void Le_sous_texte_dit_le_jour_d_ouverture_du_journal_ou_rien()
    {
        var clock = new FakeClock(Now);
        var (historique, _) = NouvelHistorique(clock);
        var journal = new FakeEtatJournal { JournalOuvertLe = new DateTimeOffset(2026, 9, 27, 6, 7, 10, TimeSpan.Zero) };
        var (fenetre, vm) = MonterReglages(sondeActivee: false, journal: journal, clock: clock,
                                           historique: historique, ouvreur: new FakeOuvreurHistorique(), section: SectionReglages.Historique);

        Assert.Equal("hebdo / 5 h / tokens · journal du 27 sept. 2026", vm.SousTexteHistorique);
        Assert.Equal(vm.SousTexteHistorique, Assert.IsType<TextBlock>(fenetre.FindName("SousTexteHistorique")).Text);

        journal.JournalOuvertLe = null;   // inconnu : le segment est OMIS, jamais inventé
        vm.ApplySnapshot(SnapshotSimple());
        Purger(fenetre);

        Assert.Equal("hebdo / 5 h / tokens", vm.SousTexteHistorique);
        Assert.Equal("hebdo / 5 h / tokens", Assert.IsType<TextBlock>(fenetre.FindName("SousTexteHistorique")).Text);
    }
}
