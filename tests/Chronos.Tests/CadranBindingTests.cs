using System.Windows;
using System.Windows.Controls;
using Chronos.Controls;
using Chronos.Models;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;
using WindowState = Chronos.Models.WindowState; // lève l'ambiguïté avec System.Windows.WindowState

namespace Chronos.Tests;

/// <summary>
/// Smoke test du cadran assemblé (05-03) : MainWindow se construit et se bind sans crash dans
/// QUATRE états (exact / estimé / indisponible / fiabilité mixte). Verrouille DAT-08 (badges
/// « estimée » PAR FENÊTRE + converter tolérant à Utilization null) et ROB-01 (deux fenêtres
/// Unavailable → cadran + texte « données indisponibles », zéro crash).
///
/// [WpfFact] (thread STA) : la construction de MainWindow + les SolidColorBrush exigent STA.
/// L'orchestrateur n'est PAS démarré (aucun I/O) ; le VM reçoit le snapshot via ApplySnapshot.
///
/// ISOLATION DES CHEMINS (phase 20, vague 0) : cette classe est antérieure à la convention
/// <c>TempPaths()</c> et montait son VM sur les chemins RÉELS du profil utilisateur. Or
/// <c>MainViewModel</c> appelle <c>settings.Load()</c> dans son constructeur : le style de cadran, le
/// mode et le thème venaient donc de la machine de celui qui lançait les tests (état constaté :
/// Arcs / Normal / ardoise). Tout test de style y aurait été vert PAR ACCIDENT et rouge chez le
/// voisin. Désormais tout passe par <see cref="TempPaths"/> : aucun test n'écrit ni ne lit dans le
/// vrai <c>%APPDATA%\Chronos</c>.
/// </summary>
[Collection("XAML WPF")]   // charge du BAML : serialise avec les autres classes XAML (voir XamlWpfCollection)
public class CadranBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Chemins de test sous <c>Path.GetTempPath()</c> : AUCUN test n'écrit dans le vrai
    /// <c>%APPDATA%\Chronos</c>, ni n'y lit ses réglages. L'assertion fait partie du motif : elle
    /// interdit qu'une régression future repointe le profil utilisateur réel.</summary>
    private static ChronosPaths TempPaths()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosCadranTest_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        Assert.StartsWith(System.IO.Path.GetTempPath(), dir);
        return new ChronosPaths(System.IO.Path.Combine(dir, "usage.json"), System.IO.Path.Combine(dir, "projects"));
    }

    // Construit un MainViewModel déterministe (orchestrateur non démarré, aucun I/O) et lui applique
    // le snapshot voulu, puis construit + met en page la fenêtre (Measure/Arrange déclenche les bindings).
    private static MainWindow BuildWindow(UsageSnapshot snap, out MainViewModel vm)
    {
        // UN SEUL répertoire temporaire pour les quatre consommateurs : SettingsFile et LastExactFile
        // sont calculés depuis UsageFile, et les DEUX SettingsService ci-dessous doivent pointer le
        // même endroit — sinon le contrôleur d'overlay relirait les réglages du profil réel.
        var paths = TempPaths();
        var prov = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(prov, paths, RefreshOptions.Default);
        var settings = new SettingsService(paths);
        vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now),
            new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(),
            settings,
            new DiagnosticService(new FakeClaudeTokenReader(), paths, settings, prov, new FakeClock(Now),
                                  machine: new FakeInventaireMachine()),
            new FakeStatusLineSetup(), new FakeOAuthLogin(), new FakeSessionsController(), new FakeAuthStatus());

        // Neutralisation EXPLICITE des deux réglages qui pilotent le rendu : aucun test de cette phase
        // ne doit dépendre d'un réglage persisté, fût-il dans un répertoire temporaire préexistant.
        // Les tests qui veulent un autre style ou le mode Étendu le posent eux-mêmes après l'appel.
        vm.CadranStyle  = CadranStyle.Arcs;   // style par défaut, posé sans ambiguïté
        vm.IsModeEtendu = false;              // mode Normal (2 anneaux)

        vm.ApplySnapshot(snap);

        var guard = new TopmostGuard();
        var controller = new OverlayController(guard, new SettingsService(paths));
        var fenetre = new MainWindow(vm, guard, controller);
        fenetre.Measure(new Size(220, 220));
        fenetre.Arrange(new Rect(0, 0, 220, 220));
        return fenetre;
    }

    [WpfFact]
    public void Etat_exact_deux_fenetres_lisibles_construit_sans_crash()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.3, ResetsAt = Now + TimeSpan.FromHours(2) },
            SevenDay = new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact, Utilization = 0.6, ResetsAt = Now + TimeSpan.FromDays(3) },
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        Assert.Same(vm, fenetre.DataContext);
        Assert.NotNull(fenetre.FindName("ArcCinqHeures") as RingArc);
        Assert.NotNull(fenetre.FindName("ArcHebdo") as RingArc);
        Assert.False(vm.DataUnavailable);
    }

    [WpfFact]
    public void Etat_estime_utilization_null_ne_crashe_pas_le_converter()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = null, ResetsAt = null },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        // Centre épuré (v1.3) : plus de badge « estimée » — l'honnêteté passe par le « ~ » du %.
        // Utilization null → PAS de % (texte vide), et le converter sur null n'a pas levé (fenêtre construite).
        Assert.True(vm.FiveHour.IsEstimated);
        Assert.Equal("", vm.FiveHour.UtilizationText);           // aucune valeur inventée
        Assert.NotNull(fenetre.FindName("ArcCinqHeures") as RingArc); // la fenêtre s'est construite sans crash
    }

    [WpfFact]
    public void Etat_indisponible_deux_fenetres_Unavailable_ne_crashe_pas_et_centre_vide()
    {
        var fenetre = BuildWindow(UsageSnapshot.Empty, out var vm);

        Assert.True(vm.DataUnavailable);                          // ROB-01
        // Centre épuré : aucune donnée → aucun % affiché (textes vides), fenêtre construite sans crash.
        Assert.Equal("", vm.FiveHour.UtilizationText);
        Assert.Equal("", vm.SevenDay.UtilizationText);
        Assert.NotNull(fenetre.FindName("ArcHebdo") as RingArc);
    }

    /// <summary>
    /// Honnêteté INDÉPENDANTE par fenêtre. RÉÉCRIT en phase 19 : la marque du chiffre non exact n'est
    /// plus le tilde « ~ » (incertitude SYMÉTRIQUE, « autour de 90 ») mais « ≥ » (incertitude
    /// UNILATÉRALE, « 90 au minimum, borne supérieure inconnue »), et elle est décidée par la
    /// PROVENANCE et non par la fiabilité. Le test n'est pas supprimé : il continue de prouver que les
    /// deux fenêtres portent des marques indépendantes.
    /// </summary>
    [WpfFact]
    public void Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.4, ResetsAt = Now + TimeSpan.FromHours(2), Provenance = ProvenanceReleve.Frais },
            SevenDay = new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated, Utilization = 0.9, ResetsAt = Now + TimeSpan.FromDays(3), Provenance = ProvenanceReleve.PlancherAvecActivite },
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        // 5 h exacte et fraîche → « 40 % », aucune marque ; hebdo plancher → « ≥ 90 % ».
        Assert.False(vm.FiveHour.IsEstimated);
        Assert.True(vm.SevenDay.IsEstimated);
        Assert.Equal("40 %", vm.FiveHour.UtilizationText);
        Assert.DoesNotContain("~", vm.SevenDay.UtilizationText);   // jamais une incertitude symétrique
        Assert.StartsWith("≥", vm.SevenDay.UtilizationText);
        Assert.Contains("90", vm.SevenDay.UtilizationText);
        Assert.NotNull(fenetre.FindName("ArcCinqHeures") as RingArc);
    }

    // NET-02 + DEL-04 : la matière première brute surfacée en texte secondaire discret, dérivée dans
    // WindowGaugeViewModel.Apply. Ces [Fact] testent directement le sous-VM (pas de STA requis : pur).
    //
    // CHANGEMENT DE CHAMP (phase 19) : les trois tests ci-dessous pilotaient « EstimatedTokens », qui
    // portait la somme de l'estimation ABSOLUE (tokens / plafond) supprimée en phase 16 — champ mort en
    // production depuis. Le chiffre de DEL-04 est « TokensDepuisReleve » : les tokens observés DEPUIS le
    // relevé exact, matière d'une borne inférieure et non d'un pourcentage. Les trois tests sont
    // RÉÉCRITS et non supprimés : ils restent la preuve que la matière brute n'est surfacée ni sur un
    // exact, ni à zéro token.

    [Fact]
    public void Plancher_avec_tokens_depuis_releve_expose_HasTokens_et_TokensText_abrege()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            Provenance = ProvenanceReleve.PlancherAvecActivite,
            TokensDepuisReleve = 62_484_658,
        });

        Assert.True(vm.HasTokens);
        Assert.Equal("≈ 62,5 M tokens", vm.TokensText);
    }

    [Fact]
    public void Exact_sans_tokens_depuis_releve_n_affiche_aucun_texte_de_tokens()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Provenance = ProvenanceReleve.Frais,
            Utilization = 0.3,
            TokensDepuisReleve = null, // honnêteté : un exact n'a aucune matière brute à exhiber
        });

        Assert.False(vm.HasTokens);
        Assert.Equal("", vm.TokensText);
    }

    [Fact]
    public void Plancher_avec_zero_token_depuis_releve_ne_surface_rien()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            Provenance = ProvenanceReleve.PlancherAvecActivite,
            TokensDepuisReleve = 0, // 0 = MESURÉ à zéro (≠ null, non mesuré) : rien à afficher non plus
        });

        Assert.False(vm.HasTokens);
        Assert.Equal("", vm.TokensText);
    }

    /// <summary>DEL-04 — garde de non-retour : l'ancien champ <c>EstimatedTokens</c>, mort en production
    /// depuis la phase 16, ne doit PLUS rien surfacer au cadran. S'il était encore lu, ce test
    /// exposerait « ≈ 99 M tokens » sous un chiffre qui n'en a pas la sémantique.</summary>
    [Fact]
    public void EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            EstimatedTokens = 99_000_000, // champ LEGACY : plus aucune lecture côté présentation
        });

        Assert.False(vm.HasTokens);
        Assert.Equal("", vm.TokensText);
    }

    // ================== TOK-02 / TOK-03 : les pastilles d'authentification ==================

    /// <summary>
    /// Monte la fenêtre dans l'état d'authentification voulu et met en page la GRILLE RACINE dans
    /// l'empreinte réelle 170x170.
    ///
    /// Pourquoi poser le DataContext sur la grille : une fenêtre jamais affichée n'a pas de template
    /// appliqué, donc son <c>Content</c> n'a AUCUN parent visuel — le DataContext ne se propage pas et
    /// aucun binding ne s'évalue (Command resterait null, Visibility resterait à son défaut Visible,
    /// et les tests seraient verts pour de mauvaises raisons). Les enfants d'un Panel, eux, SONT des
    /// enfants visuels : poser le DataContext sur la grille rétablit une évaluation RÉELLE.
    /// </summary>
    private static (MainWindow fenetre, FrameworkElement racine) MonterPastille(
        EtatAuthentification etat, out MainViewModel vm)
        => MonterPastille(etat, UsageSnapshot.Empty, out vm);

    /// <summary>
    /// Surcharge (phase 19) acceptant le SNAPSHOT : l'invitation EXA-05 dépend d'un fait porté par le
    /// snapshot (<c>UnExactADejaEteObtenu</c>) et non de l'état d'authentification. La signature
    /// historique est conservée telle quelle — les quatre tests de pastille de la phase 17 l'appellent
    /// et doivent rester verts sans retouche (leur <c>UsageSnapshot.Empty</c> porte un bit <c>null</c>,
    /// donc l'invitation y reste éteinte par construction).
    /// </summary>
    private static (MainWindow fenetre, FrameworkElement racine) MonterPastille(
        EtatAuthentification etat, UsageSnapshot snap, out MainViewModel vm)
    {
        var fenetre = BuildWindow(snap, out vm);
        vm.AppliquerEtatAuth(etat);

        var racine = (FrameworkElement)fenetre.Content!;
        racine.DataContext = vm;

        // Une réévaluation de binding déclenchée par un changement de DataContext est une opération
        // DIFFÉRÉE du Dispatcher : sans purge de la file, Command resterait null et Visibility à son
        // défaut (Visible) — le test serait vert pour de mauvaises raisons.
        racine.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        racine.Measure(new Size(170, 170));
        racine.Arrange(new Rect(0, 0, 170, 170));
        return (fenetre, racine);
    }

    /// <summary>
    /// TOK-02 : la pastille se met en page sans exception, SANS changer l'empreinte 170x170 (donc sans
    /// toucher au placement, à l'ancrage ni à OverlayController.RestorePlacement), et — surtout — sans
    /// MASQUER un anneau : son centre est mesuré à plus de 71,5 px du centre du cadran, rayon extrême
    /// de l'élément le plus externe (TickRing Radius=68 + TickLength=7). C'est la moitié automatisable
    /// de la vérification visuelle.
    /// </summary>
    [WpfFact]
    public void La_pastille_de_deconnexion_se_met_en_page_hors_de_toute_geometrie_d_anneau()
    {
        var (fenetre, racine) = MonterPastille(EtatAuthentification.Deconnecte, out var vm);
        var pastille = Assert.IsType<Button>(fenetre.FindName("PastilleDeconnexion"));

        Assert.True(vm.AfficherPastilleDeconnexion);
        Assert.False(vm.AfficherPastilleHorsLigne);
        Assert.Equal(170d, fenetre.Width);    // empreinte intacte : ancrage et placement préservés
        Assert.Equal(170d, fenetre.Height);

        // Position réelle après Arrange, exprimée dans la boîte du cadran.
        var coin = pastille.TransformToAncestor(racine).Transform(new Point(0, 0));
        var centre = new Point(coin.X + pastille.ActualWidth / 2, coin.Y + pastille.ActualHeight / 2);
        Assert.InRange(centre.X, 140d, 170d);   // bas-DROITE, dans l'empreinte
        Assert.InRange(centre.Y, 140d, 170d);

        var distanceAuCentre = (centre - new Point(85, 85)).Length;
        Assert.True(distanceAuCentre > 71.5,
            $"la pastille empiète sur la géométrie des anneaux (distance {distanceAuCentre:F1} px)");
    }

    /// <summary>
    /// TOK-03 — LE piège de la phase, verrouillé au niveau du XAML et non seulement du ViewModel :
    /// la pastille doit porter <c>ReconnecterCommand</c>. Bindée sur <c>LoginClaudeCommand</c>, qui
    /// BASCULE sur <c>IsLoggedIn == _store.Exists</c> (vrai même avec un jeton expiré), un clic
    /// SUPPRIMERAIT le coffre de jetons de l'utilisateur au lieu de le reconnecter.
    /// Le <c>Background</c> non-null est l'autre invariant : sans lui, la pastille ne serait pas
    /// hit-testable sur une fenêtre <c>AllowsTransparency</c> et le clic traverserait vers le bureau.
    /// </summary>
    [WpfFact]
    public void La_pastille_est_bindee_sur_ReconnecterCommand_et_JAMAIS_sur_LoginClaudeCommand()
    {
        var (fenetre, _) = MonterPastille(EtatAuthentification.Deconnecte, out var vm);
        var pastille = Assert.IsType<Button>(fenetre.FindName("PastilleDeconnexion"));

        Assert.Same(vm.ReconnecterCommand, pastille.Command);
        Assert.NotSame(vm.LoginClaudeCommand, pastille.Command);
        Assert.NotNull(pastille.Background);                  // hit-testable (Transparent suffit)
        Assert.Equal(Visibility.Visible, pastille.Visibility);
        Assert.Equal(HorizontalAlignment.Right, pastille.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, pastille.VerticalAlignment);
    }

    /// <summary>TOK-02 : « hors ligne » n'allume PAS la pastille actionnable. Vérifié sur les
    /// Visibility RÉSOLUES : rien à cliquer quand seul le wifi est coupé, mais l'utilisateur est tout
    /// de même prévenu par la pastille grise et inerte.</summary>
    [WpfFact]
    public void Hors_ligne_laisse_la_pastille_actionnable_COLLAPSED()
    {
        var (fenetre, _) = MonterPastille(EtatAuthentification.HorsLigne, out var vm);

        var actionnable = Assert.IsType<Button>(fenetre.FindName("PastilleDeconnexion"));
        var informative = Assert.IsType<System.Windows.Shapes.Ellipse>(fenetre.FindName("PastilleHorsLigne"));

        Assert.Equal(Visibility.Collapsed, actionnable.Visibility);
        Assert.Equal(Visibility.Visible, informative.Visibility);
        Assert.True(vm.AfficherPastilleHorsLigne);
        Assert.Equal(170d, fenetre.Width);
    }

    /// <summary>TOK-02 : à l'état CONNECTÉ, les DEUX pastilles sont éteintes — l'overlay ne porte aucun
    /// badge permanent. Contre-épreuve directe des deux tests ci-dessus.</summary>
    [WpfFact]
    public void A_l_etat_Connecte_les_DEUX_pastilles_sont_eteintes()
    {
        var (fenetre, _) = MonterPastille(EtatAuthentification.Connecte, out _);

        var actionnable = Assert.IsType<Button>(fenetre.FindName("PastilleDeconnexion"));
        var informative = Assert.IsType<System.Windows.Shapes.Ellipse>(fenetre.FindName("PastilleHorsLigne"));

        Assert.Equal(Visibility.Collapsed, actionnable.Visibility);
        Assert.Equal(Visibility.Collapsed, informative.Visibility);
    }

    // ================== EXA-05 : la pastille d'invitation à se connecter ==================

    // Snapshot « on n'a JAMAIS rien eu » : deux fenêtres indisponibles ET le bit du magasin à false.
    private static UsageSnapshot JamaisDExact(bool? bit = false) => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        UnExactADejaEteObtenu = bit,
    };

    /// <summary>
    /// EXA-05 — LE piège de la phase 17, hérité tel quel et verrouillé au niveau du XAML : l'invitation
    /// doit porter <c>ReconnecterCommand</c>. Bindée sur <c>LoginClaudeCommand</c>, qui BASCULE sur
    /// <c>IsLoggedIn == _store.Exists</c> (vrai dès que le fichier de coffre existe, même avec un jeton
    /// mort), un clic SUPPRIMERAIT le coffre de jetons de l'utilisateur.
    /// La comparaison porte sur l'INSTANCE de commande réellement bindée, pas sur un nom : un binding
    /// vers la mauvaise commande porterait le même nom de propriété dans le XAML et passerait un test
    /// textuel sans broncher.
    /// </summary>
    [WpfFact]
    public void L_invitation_est_bindee_sur_ReconnecterCommand_et_JAMAIS_sur_LoginClaudeCommand()
    {
        var (fenetre, _) = MonterPastille(EtatAuthentification.Connecte, JamaisDExact(), out var vm);
        var invitation = Assert.IsType<Button>(fenetre.FindName("PastilleInvitationConnexion"));

        Assert.True(vm.AfficherInvitationConnexion);
        Assert.Same(vm.ReconnecterCommand, invitation.Command);
        Assert.NotSame(vm.LoginClaudeCommand, invitation.Command);
        Assert.NotNull(invitation.Background);                 // hit-testable (Transparent suffit)
        Assert.Equal(Visibility.Visible, invitation.Visibility);
        Assert.Equal(HorizontalAlignment.Right, invitation.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, invitation.VerticalAlignment);

        // Exclusivité vérifiée sur les Visibility RÉSOLUES, pas seulement sur les booléens du VM.
        var deconnexion = Assert.IsType<Button>(fenetre.FindName("PastilleDeconnexion"));
        Assert.Equal(Visibility.Collapsed, deconnexion.Visibility);
    }

    /// <summary>EXA-05 — contre-épreuve : un exact a déjà été obtenu, l'invitation reste COLLAPSED. Et
    /// l'empreinte 170x170 du cadran n'a pas bougé d'un pixel : cette pastille n'est pas de la
    /// géométrie, elle est un signal posé hors de tout anneau.</summary>
    [WpfFact]
    public void L_invitation_reste_COLLAPSED_quand_un_exact_a_deja_ete_obtenu()
    {
        var (fenetre, _) = MonterPastille(EtatAuthentification.Connecte, JamaisDExact(bit: true), out var vm);
        var invitation = Assert.IsType<Button>(fenetre.FindName("PastilleInvitationConnexion"));

        Assert.False(vm.AfficherInvitationConnexion);
        Assert.Equal(Visibility.Collapsed, invitation.Visibility);
        Assert.Equal(170d, fenetre.Width);
        Assert.Equal(170d, fenetre.Height);
    }
}
