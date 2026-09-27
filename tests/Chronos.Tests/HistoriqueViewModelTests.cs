using System.ComponentModel;
using System.Globalization;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
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

        b.Vm.ChoisirVueCommand.Execute(VueHistorique.QuatreSemaines);
        Assert.Equal(VueHistorique.Jour, b.Vm.VueActive);   // phase 35 : no-op
    }

    [Fact]
    public async Task Le_changement_de_style_est_persiste_et_notifie()
    {
        var b = await Ouvert(Construire());
        Assert.Equal(HistoriqueStyleSemaine.Pistes, b.Vm.Style);
        Assert.True(b.Vm.IsStylePistes);

        var notifies = new List<string?>();
        b.Vm.PropertyChanged += (_, e) => notifies.Add(e.PropertyName);
        b.Vm.ChoisirStyleCommand.Execute(HistoriqueStyleSemaine.Tuiles);

        Assert.True(b.Vm.IsStyleTuiles);
        Assert.False(b.Vm.IsStylePistes);
        Assert.Contains(nameof(HistoriqueViewModel.IsStyleTuiles), notifies);
        Assert.Equal(HistoriqueStyleSemaine.Tuiles, b.Reglages.Courant.HistoriqueStyleSemaine);

        var deja = Construire(reglages: new ReglagesHistoriqueMemoire(new ChronosSettings { HistoriqueStyleSemaine = HistoriqueStyleSemaine.Simplifie }));
        Assert.True(deja.Vm.IsStyleSimplifie);
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
        Assert.Empty(vm.AnnotationsSauts);   // aucun trou fermé dans la journée de mercredi
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
}
