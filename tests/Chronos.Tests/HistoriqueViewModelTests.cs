using System.ComponentModel;
using System.Globalization;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Rendering.Historique;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Text;
using Chronos.ViewModels.Historique;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// HIS-08 / HIS-01 / HIS-07 côté VM — UN SEUL <see cref="HistoriqueViewModel"/> (D-34-05) : la semaine de forfait vient du repère
/// hebdo du journal (D-34-13), la lecture se fait HORS du thread UI et s'applique par <c>IUiDispatcher.Post</c> numérotée, le style
/// est persisté par <c>IReglagesHistorique</c>, la ligne de fraîcheur et l'alerte « journal muet » suivent D-32-21, le bandeau F2
/// est exact et COALESCÉ, 60 ticks de 60 s ne relisent rien et ne changent pas la référence des données, une écriture du journal
/// ou une reconstruction terminée déclenche UNE relecture, les annotations d'honnêteté et les libellés calendaires sont ceux
/// de <see cref="TextesHistorique"/>, la géométrie fait l'aller-retour, une lecture périmée est ignorée.
/// Tests simples, sans STA (aucun <c>DispatcherTimer</c> dans le ctor), fakes déterministes, horloge figée du scénario.
/// </summary>
public class HistoriqueViewModelTests
{
    private static readonly TimeZoneInfo Tz = BornesPlage.FuseauParisPourTests();
    private static readonly DateTimeOffset Now = ScenariosHistorique.Maintenant(Tz);
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private sealed record Banc(HistoriqueViewModel Vm, FakeClock Clock, FakeSourceHistorique Source, ReglagesHistoriqueMemoire Reglages,
                               FakeEtatJournal? Journal, FakeEtatReconstruction? Reconstruction, IUiDispatcher Ui);

    private static Banc Construire(IUiDispatcher? ui = null, DateTimeOffset? repere = null, bool sansRepere = false,
                                   ReglagesHistoriqueMemoire? reglages = null, bool sansJournal = false, bool sansReconstruction = false,
                                   DateTimeOffset? horlogeAuDepart = null)
    {
        var clock = new FakeClock(horlogeAuDepart ?? Now);
        var source = new FakeSourceHistorique(Tz);
        if (sansRepere) source.Repere = null; else if (repere is { } r) source.Repere = r;
        reglages ??= new ReglagesHistoriqueMemoire();
        var journal = sansJournal ? null : new FakeEtatJournal();
        var reconstruction = sansReconstruction ? null : new FakeEtatReconstruction();
        ui ??= new FakeUiDispatcher { OnUiThread = true };
        var vm = new HistoriqueViewModel(source, ui, clock, Tz, reglages, journal, reconstruction);
        return new Banc(vm, clock, source, reglages, journal, reconstruction, ui);
    }

    private static async Task<Banc> Ouvert(Banc b)
    {
        b.Vm.Ouvrir();
        await b.Vm.AttendreLecture();
        return b;
    }

    [Fact]
    public async Task L_ouverture_calcule_la_semaine_depuis_le_repere_et_lit_hors_UI()
    {
        var b = await Ouvert(Construire());

        Assert.Equal(VueHistorique.Semaine, b.Vm.VueActive);
        Assert.True(b.Vm.IsVueSemaine);
        Assert.Equal(ScenariosHistorique.Semaine(Tz), b.Vm.PlageCourante);
        Assert.Equal(1, b.Source.Lectures);
        Assert.True(((FakeUiDispatcher)b.Ui).PostCount >= 1);
        Assert.NotNull(b.Vm.DonneesSemaine);
        Assert.Equal("Semaine de forfait · sam. 19 sept. 00:00 → sam. 26 sept. 00:00", b.Vm.LibellePeriode);
        Assert.True(b.Vm.EstAuPresent);
        Assert.Equal("Cette semaine", b.Vm.TexteRetourPresent);
    }

    [Fact]
    public async Task Sans_repere_la_semaine_vient_de_l_ancre_puis_du_calendrier()
    {
        var ancre = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.FromHours(2));
        var avecAncre = await Ouvert(Construire(sansRepere: true, reglages: new ReglagesHistoriqueMemoire(new ChronosSettings { WeeklyAnchor = ancre })));
        Assert.Equal(BornesPlage.SemaineDeForfait(Now, null, ancre, Tz), avecAncre.Vm.PlageCourante);

        var sansAncre = await Ouvert(Construire(sansRepere: true));
        Assert.Equal(BornesPlage.SemaineDeForfait(Now, null, null, Tz), sansAncre.Vm.PlageCourante);
    }

    [Fact]
    public async Task Precedent_recule_d_une_semaine_et_Suivant_est_interdit_au_present()
    {
        var b = await Ouvert(Construire());
        var initiale = b.Vm.PlageCourante!;
        Assert.False(b.Vm.SuivantCommand.CanExecute(null));

        b.Vm.PrecedentCommand.Execute(null);
        await b.Vm.AttendreLecture();
        Assert.Equal(initiale.Debut, b.Vm.PlageCourante!.Fin);
        Assert.False(b.Vm.EstAuPresent);
        Assert.StartsWith("Semaine de forfait · sam. 12 sept.", b.Vm.LibellePeriode, StringComparison.Ordinal);
        Assert.Equal(2, b.Source.Lectures);
        Assert.Equal(b.Vm.PlageCourante, b.Vm.DonneesSemaine!.Plage);

        Assert.True(b.Vm.SuivantCommand.CanExecute(null));
        b.Vm.SuivantCommand.Execute(null);
        await b.Vm.AttendreLecture();
        Assert.Equal(initiale, b.Vm.PlageCourante);
        Assert.True(b.Vm.EstAuPresent);
        Assert.Equal(3, b.Source.Lectures);

        b.Vm.PrecedentCommand.Execute(null);
        b.Vm.PrecedentCommand.Execute(null);
        await b.Vm.AttendreLecture();
        Assert.Equal(5, b.Source.Lectures);
        b.Vm.RetourPresentCommand.Execute(null);
        await b.Vm.AttendreLecture();
        Assert.Equal(initiale, b.Vm.PlageCourante);
        Assert.True(b.Vm.EstAuPresent);
        Assert.Equal(6, b.Source.Lectures);
    }

    [Fact]
    public async Task La_vue_Jour_lit_le_jour_local_et_navigue_par_jour()
    {
        var b = await Ouvert(Construire());

        b.Vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        await b.Vm.AttendreLecture();
        Assert.True(b.Vm.IsVueJour);
        Assert.False(b.Vm.IsVueSemaine);
        Assert.Equal(BornesPlage.Jour(Now, Tz), b.Vm.PlageCourante);
        Assert.NotNull(b.Vm.DonneesJour);
        Assert.Equal("Jour · jeudi 24 sept. 2026 · semaine de forfait du 19 sept.", b.Vm.LibellePeriode);
        Assert.Equal("Aujourd'hui", b.Vm.TexteRetourPresent);
        Assert.True(b.Vm.AfficherMaintenant);

        b.Vm.PrecedentCommand.Execute(null);
        await b.Vm.AttendreLecture();
        Assert.Equal(BornesPlage.Jour(Now - TimeSpan.FromDays(1), Tz), b.Vm.PlageCourante);
        Assert.False(b.Vm.AfficherMaintenant);
        Assert.False(b.Vm.EstAuPresent);
    }

    /// <summary>Phase 38 (HIS-09) : un seul style (Pistes) — ni le VM ni les réglages n'exposent plus de style de la vue Semaine.</summary>
    [Fact]
    public void Le_vm_n_expose_plus_de_style()
    {
        Assert.Null(typeof(HistoriqueViewModel).GetProperty("Style"));
        Assert.Null(typeof(HistoriqueViewModel).GetProperty("ChoisirStyleCommand"));
        Assert.Null(typeof(HistoriqueViewModel).GetProperty("IsStyleTuiles"));
        Assert.Null(typeof(ChronosSettings).GetProperty("HistoriqueStyleSemaine"));
    }

    [Fact]
    public async Task La_ligne_de_fraicheur_est_celle_des_textes()
    {
        var b = await Ouvert(Construire());

        Assert.Equal(TextesHistorique.LigneFraicheurSemaine(b.Vm.DonneesSemaine!.Analyse, Now, Tz), b.Vm.TexteFraicheur);
        Assert.StartsWith("Dernier relevé il y a 2 min · sonde d'en-têtes de rate-limit · ", b.Vm.TexteFraicheur, StringComparison.Ordinal);
        Assert.EndsWith(" · 2 interruptions · journal ouvert le 14 sept. 2026", b.Vm.TexteFraicheur, StringComparison.Ordinal);

        b.Vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        await b.Vm.AttendreLecture();
        Assert.StartsWith("288 relevés attendus · ", b.Vm.TexteFraicheur, StringComparison.Ordinal);
        Assert.Contains("(jeton invalide, 14:00 → 16:00)", b.Vm.TexteFraicheur, StringComparison.Ordinal);
    }

    [Fact]
    public async Task L_alerte_journal_muet_suit_la_regle_D_32_21()
    {
        // Chronos tourne depuis 30 min : une écriture vieille de 16 min est un silence, de 14 min non.
        var b = await Ouvert(Construire(horlogeAuDepart: Now - TimeSpan.FromMinutes(30)));
        b.Clock.UtcNow = Now;
        b.Journal!.DerniereEcriture = Now - TimeSpan.FromMinutes(16);
        b.Vm.Tick(Now);
        Assert.True(b.Vm.AlerteJournal);
        Assert.Equal("journal muet depuis 16 min", b.Vm.TexteAlerteJournal);

        b.Journal.DerniereEcriture = Now - TimeSpan.FromMinutes(14);
        b.Vm.Tick(Now);
        Assert.False(b.Vm.AlerteJournal);
        Assert.Equal("", b.Vm.TexteAlerteJournal);

        // Juste après le démarrage : ni « aucune écriture » ni l'écriture de la veille ne sont un silence (max(démarrage, dernière)).
        var frais = await Ouvert(Construire());
        Assert.Null(frais.Journal!.DerniereEcriture);
        Assert.False(frais.Vm.AlerteJournal);
        frais.Journal.DerniereEcriture = Now - TimeSpan.FromMinutes(16);
        frais.Vm.Tick(Now);
        Assert.False(frais.Vm.AlerteJournal);
        frais.Journal.DerniereEcriture = null;
        frais.Vm.Tick(Now + TimeSpan.FromMinutes(16));
        Assert.True(frais.Vm.AlerteJournal);

        var sansJournal = await Ouvert(Construire(sansJournal: true));
        sansJournal.Vm.Tick(Now + TimeSpan.FromHours(3));
        Assert.False(sansJournal.Vm.AlerteJournal);
        Assert.Equal("", sansJournal.Vm.TexteAlerteJournal);
    }

    [Fact]
    public async Task Soixante_ticks_ne_changent_pas_les_donnees_des_pistes()
    {
        var b = await Ouvert(Construire());
        var donnees = b.Vm.DonneesSemaine;
        Assert.NotNull(donnees);
        var notifs = 0;
        b.Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HistoriqueViewModel.DonneesSemaine) or nameof(HistoriqueViewModel.DonneesJour)
                or nameof(HistoriqueViewModel.LibellesJours) or nameof(HistoriqueViewModel.AnnotationsTrous))
                notifs++;
        };

        for (var i = 0; i < 60; i++)
        {
            b.Clock.UtcNow += TimeSpan.FromMinutes(1);
            b.Vm.Tick(b.Clock.UtcNow);
        }
        await b.Vm.AttendreLecture();

        Assert.Same(donnees, b.Vm.DonneesSemaine);
        Assert.Equal(0, notifs);
        Assert.Equal(1, b.Source.Lectures);
        Assert.Equal(b.Clock.UtcNow, b.Vm.Maintenant);
        Assert.StartsWith("Dernier relevé il y a 1 h 02", b.Vm.TexteFraicheur, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Une_nouvelle_ecriture_du_journal_declenche_une_relecture_au_tick()
    {
        var b = await Ouvert(Construire());
        var donnees = b.Vm.DonneesSemaine;

        b.Journal!.DerniereEcriture = Now + TimeSpan.FromMinutes(1);
        b.Vm.Tick(Now + TimeSpan.FromMinutes(1));
        await b.Vm.AttendreLecture();

        Assert.Equal(2, b.Source.Lectures);
        Assert.False(ReferenceEquals(donnees, b.Vm.DonneesSemaine));

        b.Vm.Tick(Now + TimeSpan.FromMinutes(2));
        await b.Vm.AttendreLecture();
        Assert.Equal(2, b.Source.Lectures);
    }

    [Fact]
    public async Task Une_reconstruction_terminee_declenche_une_relecture_au_tick()
    {
        var b = await Ouvert(Construire());

        b.Reconstruction!.DerniereReconstructionTerminee = Now + TimeSpan.FromSeconds(30);
        b.Vm.Tick(Now + TimeSpan.FromMinutes(1));
        await b.Vm.AttendreLecture();

        Assert.Equal(2, b.Source.Lectures);
    }

    [Fact]
    public async Task Le_bandeau_F2_est_visible_en_reconstruction_et_disparait_ensuite()
    {
        var b = Construire();
        b.Reconstruction!.Phase = PhaseReconstruction.Reconstruction;
        b.Reconstruction.FichiersTraites = 886;
        b.Reconstruction.FichiersTotal = 1603;
        b.Reconstruction.SemaineCouranteDisponible = true;
        await Ouvert(b);

        Assert.True(b.Vm.AfficherBandeauF2);
        Assert.Equal("Reconstruction des tokens depuis vos transcripts Claude Code — 886 / 1603 fichiers · la semaine courante est déjà complète", b.Vm.TexteBandeauF2);
        Assert.Equal(886.0 / 1603, b.Vm.FractionBandeauF2, 9);
        Assert.EndsWith("ils commencent au 14 sept. 2026", b.Vm.SousTexteBandeauF2, StringComparison.Ordinal);

        b.Reconstruction.Phase = PhaseReconstruction.Incremental;
        b.Reconstruction.Declencher();
        Assert.False(b.Vm.AfficherBandeauF2);

        b.Reconstruction.Phase = PhaseReconstruction.Arretee;
        b.Reconstruction.Declencher();
        Assert.False(b.Vm.AfficherBandeauF2);

        var sans = await Ouvert(Construire(sansReconstruction: true));
        Assert.False(sans.Vm.AfficherBandeauF2);
        Assert.Equal("", sans.Vm.TexteBandeauF2);
    }

    [Fact]
    public async Task Cinquante_changements_ne_franchissent_la_frontiere_UI_qu_une_fois()
    {
        var ui = new FakeUiDispatcherDiffere { OnUiThread = true };
        var b = Construire(ui: ui);
        b.Reconstruction!.Phase = PhaseReconstruction.Reconstruction;
        b.Reconstruction.FichiersTraites = 886;
        b.Reconstruction.FichiersTotal = 1603;
        b.Reconstruction.SemaineCouranteDisponible = true;
        await Ouvert(b);
        ui.Vider();
        Assert.NotNull(b.Vm.DonneesSemaine);
        var avant = ui.PostCount;

        await Task.Run(() => { for (var i = 0; i < 50; i++) b.Reconstruction.Declencher(); });
        Assert.Equal(avant + 1, ui.PostCount);

        b.Reconstruction.FichiersTraites = 900;
        ui.Vider();
        Assert.Contains("900 / 1603", b.Vm.TexteBandeauF2, StringComparison.Ordinal);

        b.Reconstruction.Declencher();
        Assert.Equal(avant + 2, ui.PostCount);   // le drapeau est retombé au vidage
    }

    [Fact]
    public async Task Les_annotations_disent_les_trous_les_sauts_l_ouverture_et_la_divergence()
    {
        var b = await Ouvert(Construire());
        var vm = b.Vm;

        Assert.Equal(2, vm.AnnotationsTrous.Count);
        Assert.Equal("Chronos arrêté", vm.AnnotationsTrous[0].Texte);
        Assert.Equal(CauseTrou.ChronosArrete, vm.AnnotationsTrous[0].Cause);
        Assert.Equal(Utc("2026-09-22T21:00:00Z"), vm.AnnotationsTrous[0].Debut);
        Assert.Equal(Utc("2026-09-23T05:00:00Z"), vm.AnnotationsTrous[0].Fin);
        Assert.Equal("jeton invalide", vm.AnnotationsTrous[1].Texte);
        Assert.Equal(CauseTrou.JetonInvalide, vm.AnnotationsTrous[1].Cause);
        Assert.All(vm.AnnotationsTrous, a => Assert.Equal(TypeAnnotation.Trou, a.Type));

        Assert.Contains(vm.AnnotationsSauts, a => a.Texte == "+2 % pendant l'absence (répartition inconnue)" && a.Debut == Utc("2026-09-22T21:00:00Z"));
        Assert.DoesNotContain(vm.AnnotationsSauts, a => a.Texte.StartsWith("au moins un reset", StringComparison.Ordinal));   // en Semaine : hebdo seulement
        Assert.All(vm.AnnotationsSauts, a => Assert.Equal(TypeAnnotation.Saut, a.Type));

        Assert.Null(vm.AnnotationJournalOuvert);   // ouvert le 14 sept., avant la semaine
        Assert.True(vm.AfficherPiedDivergence);
        Assert.Equal(TextesHistorique.PiedDivergence, vm.TextePiedDivergence);
        var divergence = Assert.Single(vm.AnnotationsDivergences);
        Assert.Equal(Utc("2026-09-23T19:00:00Z"), divergence.Debut);
        Assert.Equal(Utc("2026-09-23T21:00:00Z"), divergence.Fin);

        vm.PrecedentCommand.Execute(null);
        await vm.AttendreLecture();
        Assert.NotNull(vm.AnnotationJournalOuvert);
        Assert.Equal("journal ouvert le 14 sept. 2026", vm.AnnotationJournalOuvert!.Texte);
        Assert.Equal(ScenariosHistorique.JournalOuvertLe, vm.AnnotationJournalOuvert.Debut);
        Assert.Equal(TypeAnnotation.JournalOuvert, vm.AnnotationJournalOuvert.Type);

        // En Jour (aujourd'hui) : le saut du trou « jeton invalide » est celui de la fenêtre 5 h (reset dans l'absence).
        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        await vm.AttendreLecture();
        var saut = Assert.Single(vm.AnnotationsSauts);
        Assert.Equal("au moins un reset pendant l'absence (répartition inconnue)", saut.Texte);

        var sansDivergence = Construire();
        sansDivergence.Source.TransformerSemaine = d => d with { Divergences = Array.Empty<Divergence>() };
        await Ouvert(sansDivergence);
        Assert.False(sansDivergence.Vm.AfficherPiedDivergence);
        Assert.Empty(sansDivergence.Vm.AnnotationsDivergences);
    }

    [Fact]
    public async Task La_vue_Jour_annonce_les_resets_et_l_epuisee()
    {
        var b = await Ouvert(Construire());
        var vm = b.Vm;
        Assert.StartsWith("par heure, ", vm.LibellePermanentTokens, StringComparison.Ordinal);
        Assert.Equal("", vm.LegendeModeles);

        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        await vm.AttendreLecture();
        Assert.StartsWith("par quart d'heure, ", vm.LibellePermanentTokens, StringComparison.Ordinal);
        Assert.Equal("opus · sonnet · haiku · sous-agents inclus", vm.LegendeModeles);
        Assert.StartsWith("0 – ", vm.EchelleTokens, StringComparison.Ordinal);
        Assert.EndsWith(" (sortie)", vm.EchelleTokens, StringComparison.Ordinal);
        Assert.True(vm.PlafondTokens > 0);
        Assert.Same(Tz, vm.Fuseau);

        vm.PrecedentCommand.Execute(null);   // mer. 23 sept.
        await vm.AttendreLecture();
        Assert.Contains(vm.AnnotationsResets, a => a.Texte == "reset 5 h 14:00" && a.Debut == Utc("2026-09-23T12:00:00Z"));
        Assert.Contains(vm.AnnotationsResets, a => a.Texte == "reset 5 h 19:00" && a.Debut == Utc("2026-09-23T17:00:00Z"));
        Assert.All(vm.AnnotationsResets, a => { Assert.Equal(TypeAnnotation.Reset5h, a.Type); Assert.Null(a.Fin); Assert.True(vm.PlageCourante!.Contient(a.Debut)); });

        var epuisee = Assert.Single(vm.AnnotationsEpuisee);
        Assert.Equal(TypeAnnotation.Epuisee, epuisee.Type);
        Assert.Equal(Utc("2026-09-23T18:00:00Z"), epuisee.Debut);
        Assert.Equal(Utc("2026-09-23T22:00:00Z"), epuisee.Fin);
        Assert.Equal(TextesHistorique.Epuisee, epuisee.Texte);
        // 35-01 : la veille est lue, le trou de minuit est nommé — il se FERME dans l'analyse du jour ; son saut 5 h traverse le reset de
        // 04:00 : indéterminable, et dit comme tel (jamais une barre au réveil).
        var saut = Assert.Single(vm.AnnotationsSauts);
        Assert.Equal("au moins un reset pendant l'absence (répartition inconnue)", saut.Texte);
        Assert.Equal(Utc("2026-09-22T21:00:00Z"), saut.Debut);
        Assert.Equal(Utc("2026-09-23T05:00:00Z"), saut.Fin);
        Assert.Empty(vm.AnnotationsDivergences);
        Assert.False(vm.AfficherPiedDivergence);
    }

    [Fact]
    public async Task Les_libelles_des_jours_et_des_heures_suivent_le_calendrier_local()
    {
        var b = await Ouvert(Construire());

        Assert.Equal(7, b.Vm.LibellesJours.Count);
        Assert.Equal("sam. 19", b.Vm.LibellesJours[0].Texte);
        Assert.Equal("ven. 25", b.Vm.LibellesJours[6].Texte);
        Assert.Equal(Utc("2026-09-18T22:00:00Z"), b.Vm.LibellesJours[0].Instant);

        b.Vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        await b.Vm.AttendreLecture();
        Assert.Equal(8, b.Vm.LibellesHeures.Count);
        Assert.Equal("0 h", b.Vm.LibellesHeures[0].Texte);
        Assert.Equal("3 h", b.Vm.LibellesHeures[1].Texte);
        Assert.Equal("21 h", b.Vm.LibellesHeures[7].Texte);
        Assert.Equal(b.Vm.PlageCourante!.Debut, b.Vm.LibellesHeures[0].Instant);
    }

    [Fact]
    public async Task La_geometrie_persistee_fait_l_aller_retour_et_Fermer_leve_l_evenement()
    {
        var b = await Ouvert(Construire());
        Assert.Equal((null, null, null, null), b.Vm.GeometriePersistee());

        b.Vm.EnregistrerGeometrie(100, 50, 900, 600);
        Assert.Equal(100, b.Reglages.Courant.HistoriqueX);
        Assert.Equal(50, b.Reglages.Courant.HistoriqueY);
        Assert.Equal(900, b.Reglages.Courant.HistoriqueWidth);
        Assert.Equal(600, b.Reglages.Courant.HistoriqueHeight);

        var relu = Construire(reglages: b.Reglages);
        Assert.Equal((100, 50, 900, 600), relu.Vm.GeometriePersistee());

        var ferme = 0;
        b.Vm.FermetureDemandee += (_, _) => ferme++;
        b.Vm.FermerCommand.Execute(null);
        Assert.Equal(1, ferme);

        Assert.Equal(b.Reglages.Courant.ThemeKey, b.Vm.Theme.Key);
        Assert.Equal("minuit", b.Vm.Theme.Key);
        Assert.Equal(TextesHistorique.PiedDePage, b.Vm.PiedDePage);
        Assert.Equal(TextesHistorique.EchelleRythme, b.Vm.EchelleRythme);
        Assert.Equal(TextesHistorique.LegendeTokens, b.Vm.LegendeTokens);
    }

    [Fact]
    public async Task Une_lecture_perimee_n_ecrase_pas_la_plus_recente()
    {
        var b = Construire();
        b.Source.Barriere = new ManualResetEventSlim(false);

        b.Vm.Ouvrir();                      // lecture 1 (semaine courante), bloquée
        b.Vm.PrecedentCommand.Execute(null); // lecture 2 (semaine précédente), bloquée aussi
        var attendue = b.Vm.PlageCourante!;
        b.Source.Barriere.Set();
        await b.Vm.AttendreLecture();

        Assert.Equal(2, b.Source.Lectures);
        Assert.NotNull(b.Vm.DonneesSemaine);
        Assert.Equal(attendue, b.Vm.DonneesSemaine!.Plage);
        Assert.NotEqual(ScenariosHistorique.Semaine(Tz), b.Vm.DonneesSemaine.Plage);
    }

    // ------------------------------------------------------------------ 35-01 : vue 4 semaines (HIS-05) et thème relu

    private static readonly string[] JoursCourts = { "sam.", "dim.", "lun.", "mar.", "mer.", "jeu.", "ven." };

    private static async Task<Banc> QuatreSemainesOuvertes(Banc b)
    {
        await Ouvert(b);
        b.Vm.ChoisirVueCommand.Execute(VueHistorique.QuatreSemaines);
        await b.Vm.AttendreLecture();
        return b;
    }

    [Fact]
    public async Task La_vue_quatre_semaines_s_ouvre_sur_le_bloc_courant()
    {
        var b = await QuatreSemainesOuvertes(Construire());
        var vm = b.Vm;

        Assert.Equal(VueHistorique.QuatreSemaines, vm.VueActive);
        Assert.True(vm.IsVueQuatreSemaines);
        Assert.False(vm.IsVueSemaine);
        Assert.False(vm.IsVueJour);
        Assert.Equal(new Plage(Utc("2026-08-28T22:00:00Z"), Utc("2026-09-25T22:00:00Z")), vm.PlageCourante);
        Assert.Equal("4 semaines de forfait · du sam. 29 août au sam. 26 sept. 2026", vm.LibellePeriode);
        Assert.Equal("Cette semaine", vm.TexteRetourPresent);
        Assert.False(vm.AfficherMaintenant);
        Assert.True(vm.EstAuPresent);

        var d = Assert.IsType<DonneesQuatreSemaines>(vm.DonneesQuatreSemaines);
        Assert.Equal(4, d.Semaines.Count);
        Assert.Equal(ScenariosHistorique.Semaine(Tz), d.Courante.Plage);
        Assert.Equal(JoursCourts, vm.LibellesJoursCourts.Select(g => g.Texte));
        Assert.Equal(GraduationsCalendrier.Jours(ScenariosHistorique.Semaine(Tz), Tz), vm.LibellesJoursCourts.Select(g => g.Instant));
        Assert.Equal(TextesHistorique.PiedQuatreSemaines, vm.PiedQuatreSemaines);

        // Depuis la vue Jour aussi : le segment n'est plus ignoré.
        vm.ChoisirVueCommand.Execute(VueHistorique.Jour);
        await vm.AttendreLecture();
        vm.ChoisirVueCommand.Execute(VueHistorique.QuatreSemaines);
        await vm.AttendreLecture();
        Assert.True(vm.IsVueQuatreSemaines);
        Assert.Equal(new Plage(Utc("2026-08-28T22:00:00Z"), Utc("2026-09-25T22:00:00Z")), vm.PlageCourante);
    }

    [Fact]
    public async Task La_navigation_quatre_semaines_avance_et_recule_par_bloc()
    {
        var b = await QuatreSemainesOuvertes(Construire());
        var vm = b.Vm;
        var courant = vm.PlageCourante!;
        Assert.False(vm.SuivantCommand.CanExecute(null));

        vm.PrecedentCommand.Execute(null);
        await vm.AttendreLecture();
        Assert.Equal(new Plage(Utc("2026-07-31T22:00:00Z"), Utc("2026-08-28T22:00:00Z")), vm.PlageCourante);   // sam. 1er août → sam. 29 août
        Assert.False(vm.EstAuPresent);
        Assert.Equal("4 semaines de forfait · du sam. 1 août au sam. 29 août 2026", vm.LibellePeriode);
        Assert.Equal(vm.PlageCourante!.Debut, vm.DonneesQuatreSemaines!.Semaines[0].Plage.Debut);
        Assert.Equal(vm.PlageCourante.Fin, vm.DonneesQuatreSemaines.Courante.Plage.Fin);
        Assert.True(vm.SuivantCommand.CanExecute(null));

        vm.SuivantCommand.Execute(null);
        await vm.AttendreLecture();
        Assert.Equal(courant, vm.PlageCourante);
        Assert.True(vm.EstAuPresent);
        Assert.False(vm.SuivantCommand.CanExecute(null));
        Assert.Equal(BornesPlage.QuatreSemaines(Now, ScenariosHistorique.RepereHebdo, null, Tz), vm.DonneesQuatreSemaines!.Semaines.Select(a => a.Plage));
    }

    [Fact]
    public async Task Les_etiquettes_distinguent_avant_le_journal_et_pas_de_releves()
    {
        var b = await QuatreSemainesOuvertes(Construire());
        var e = b.Vm.EtiquettesSemaines;
        var d = b.Vm.DonneesQuatreSemaines!;

        Assert.Equal(new[] { 0, 1, 2, 3 }, e.Select(x => x.Rang));
        Assert.Equal("S · 19 sept. · " + TextesHistorique.Pourcent(d.Courante.Serie.Last(r => r.U7 is not null).U7), e[0].Texte);
        Assert.EndsWith(" %", e[0].Texte, StringComparison.Ordinal);
        Assert.StartsWith("S-1 · 12 sept. · ", e[1].Texte, StringComparison.Ordinal);
        Assert.EndsWith(" %", e[1].Texte, StringComparison.Ordinal);
        Assert.False(e[0].AvantJournal);
        Assert.False(e[1].AvantJournal);
        Assert.Equal("S-2 · 5 sept. · pas de relevés (avant le journal)", e[2].Texte);
        Assert.Equal("S-3 · 29 août · pas de relevés (avant le journal)", e[3].Texte);
        Assert.True(e[2].AvantJournal);
        Assert.True(e[3].AvantJournal);

        // Une semaine POSTÉRIEURE à l'ouverture du journal, sans relevé : « pas de relevés », jamais « avant le journal » (D-35-03).
        var vide = Construire();
        vide.Source.TransformerQuatreSemaines = q => q with
        {
            Semaines = new[]
            {
                q.Semaines[0], q.Semaines[1],
                AnalyseReleves.Analyser(new LectureJournal(Array.Empty<ReleveJournal>(), Array.Empty<EvenementJournal>(), 0, q.JournalOuvertLe, q.Semaines[2].Plage),
                                        q.Semaines[2].Plage.Fin, RateLimitHeaderUsageProvider.CadenceNominale),
                q.Semaines[3],
            },
        };
        await QuatreSemainesOuvertes(vide);
        var s1 = vide.Vm.EtiquettesSemaines[1];
        Assert.Equal("S-1 · 12 sept. · pas de relevés", s1.Texte);
        Assert.DoesNotContain("avant le journal", s1.Texte, StringComparison.Ordinal);
        Assert.False(s1.AvantJournal);
        Assert.Equal(TextesHistorique.AucunReleve, vide.Vm.RangeesCouverture[1].Texte);
    }

    [Fact]
    public async Task La_semaine_epuisee_est_annotee_et_reportee_sur_l_axe_de_S()
    {
        var sans = await QuatreSemainesOuvertes(Construire());
        Assert.Empty(sans.Vm.AnnotationsEpuiseeSemaines);

        var b = Construire();
        b.Source.TransformerQuatreSemaines = q => FixturesQuatreSemaines.SemaineUnEpuisee(q, Tz);
        await QuatreSemainesOuvertes(b);
        var d = b.Vm.DonneesQuatreSemaines!;
        var premier = d.Semaines[2].Serie.First(r => r.T >= FixturesQuatreSemaines.DebutEpuisee);

        var a = Assert.Single(b.Vm.AnnotationsEpuiseeSemaines);
        Assert.Equal(TypeAnnotation.Epuisee, a.Type);
        Assert.Equal(TextesHistorique.EpuiseeSemaine(premier.T, Tz), a.Texte);
        Assert.Equal("épuisée jeu. 20:00 → bloquée jusqu'au reset", a.Texte);
        Assert.Equal(EchelleTemps.Reporter(premier.T, d.Semaines[2].Plage, d.Courante.Plage), a.Debut);
        Assert.Equal(d.Courante.Plage.Fin, a.Fin);
        Assert.True(d.Courante.Plage.Contient(a.Debut));
    }

    [Fact]
    public async Task Les_rangees_de_couverture_disent_avant_le_journal_et_marquent_l_ouverture()
    {
        var b = await QuatreSemainesOuvertes(Construire());
        var r = b.Vm.RangeesCouverture;
        var d = b.Vm.DonneesQuatreSemaines!;

        Assert.Equal(4, r.Count);
        Assert.Equal(new[] { "S", "S-1", "S-2", "S-3" }, r.Select(x => x.Libelle));
        Assert.Equal(new[] { 0, 1, 2, 3 }, r.Select(x => x.Rang));
        for (var k = 0; k < 4; k++) Assert.Equal(d.Semaines[3 - k].Plage, r[k].Plage);

        foreach (var k in new[] { 2, 3 })
        {
            Assert.Equal(r[k].Plage, r[k].ZoneAvantJournal);
            Assert.Equal(TextesHistorique.AvantJournalAucunReleve, r[k].Texte);
            Assert.Null(r[k].JournalOuvert);
            Assert.Empty(r[k].Serie);
        }

        Assert.Equal(new Plage(r[1].Plage.Debut, ScenariosHistorique.JournalOuvertLe), r[1].ZoneAvantJournal);
        Assert.Equal("journal ouvert le 14 sept. 2026", r[1].JournalOuvert!.Texte);
        Assert.Equal(ScenariosHistorique.JournalOuvertLe, r[1].JournalOuvert!.Debut);
        Assert.Equal("", r[1].Texte);
        Assert.Equal(r[1].Plage.Fin, r[1].InstantLecture);
        Assert.NotEmpty(r[1].Serie);

        Assert.Null(r[0].ZoneAvantJournal);
        Assert.Null(r[0].JournalOuvert);
        Assert.Equal("", r[0].Texte);
        Assert.Equal(d.LueA, r[0].InstantLecture);
        Assert.Equal(2, r[0].Trous.Count);   // « Chronos arrêté » et « jeton invalide »
    }

    [Fact]
    public async Task La_fraicheur_quatre_semaines_est_celle_de_la_semaine_courante()
    {
        var b = await QuatreSemainesOuvertes(Construire());

        Assert.Equal(TextesHistorique.LigneFraicheurSemaine(b.Vm.DonneesQuatreSemaines!.Courante, Now, Tz), b.Vm.TexteFraicheur);
        Assert.Equal("Rien n'est inventé avant l'ouverture du journal.", b.Vm.PiedQuatreSemaines);
    }

    [Fact]
    public void Le_theme_se_relit_a_la_demande()
    {
        var b = Construire(reglages: new ReglagesHistoriqueMemoire(new ChronosSettings { ThemeKey = "nord" }));
        Assert.Equal("nord", b.Vm.Theme.Key);

        var notifies = new List<string?>();
        b.Vm.PropertyChanged += (_, e) => notifies.Add(e.PropertyName);
        b.Reglages.Modifier(s => s with { ThemeKey = "aurore" });
        Assert.Equal("nord", b.Vm.Theme.Key);   // rien n'est relu tant qu'on ne le demande pas

        b.Vm.ActualiserTheme();
        Assert.Equal("aurore", b.Vm.Theme.Key);
        Assert.Contains(nameof(HistoriqueViewModel.Theme), notifies);
    }

    // ------------------------------------------------------------------ Plein écran (HIS-10 / HIS-11)

    [Fact]
    public async Task Le_plein_ecran_est_faux_au_depart_et_bascule()
    {
        var b = await Ouvert(Construire());
        Assert.False(b.Vm.EstPleinEcran);

        var notifies = new List<string?>();
        b.Vm.PropertyChanged += (_, e) => notifies.Add(e.PropertyName);

        b.Vm.BasculerPleinEcranCommand.Execute(null);
        Assert.True(b.Vm.EstPleinEcran);
        Assert.Contains(nameof(HistoriqueViewModel.EstPleinEcran), notifies);

        b.Vm.BasculerPleinEcranCommand.Execute(null);
        Assert.False(b.Vm.EstPleinEcran);
    }

    [Fact]
    public async Task Echap_quitte_d_abord_le_plein_ecran_puis_ferme_la_fenetre()
    {
        var b = await Ouvert(Construire());
        var ferme = 0;
        b.Vm.FermetureDemandee += (_, _) => ferme++;

        // Hors plein écran : Échap ferme directement, une seule fois.
        b.Vm.EchapCommand.Execute(null);
        Assert.Equal(1, ferme);

        // En plein écran : le premier Échap quitte le plein écran sans fermer, le second ferme.
        ferme = 0;
        b.Vm.BasculerPleinEcranCommand.Execute(null);
        b.Vm.EchapCommand.Execute(null);
        Assert.False(b.Vm.EstPleinEcran);
        Assert.Equal(0, ferme);

        b.Vm.EchapCommand.Execute(null);
        Assert.Equal(1, ferme);
        Assert.False(b.Vm.EstPleinEcran);
    }

    [Fact]
    public async Task Le_plein_ecran_n_est_jamais_persiste()
    {
        var b = await Ouvert(Construire());
        var avant = b.Reglages.Courant;

        b.Vm.BasculerPleinEcranCommand.Execute(null);
        Assert.Equal(avant, b.Reglages.Courant);
        b.Vm.BasculerPleinEcranCommand.Execute(null);
        Assert.Equal(avant, b.Reglages.Courant);
    }
}
