using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Quick 260927-reglages-v2 — l'état PROPRE de la fenêtre de réglages, sans WPF : la section courante (défaut, persistance,
/// relecture à l'ouverture, Ctrl+1…6), la géométrie mémorisée et le panneau « Diagnostic » (génération asynchrone, erreur lisible,
/// copie). Réglages en MÉMOIRE (<see cref="ReglagesHistoriqueMemoire"/>) : aucun test n'écrit un <c>settings.json</c>.
/// </summary>
public class ReglagesViewModelTests
{
    // 19:04 UTC le 27 septembre 2026 = 21:04 à Paris (heure d'été) : l'heure de la maquette R3.
    private static readonly DateTimeOffset Maintenant = new(2026, 9, 27, 19, 4, 12, TimeSpan.Zero);

    private static ReglagesViewModel Nouveau(ReglagesHistoriqueMemoire reglages, Func<Task<string>>? generer = null,
                                             FakePressePapiers? pressePapiers = null)
        => new(reglages, generer ?? (() => Task.FromResult("=== Chronos — Diagnostic ===")), new FakeClock(Maintenant),
               pressePapiers, BornesPlage.FuseauParisPourTests());

    // ------------------------------------------------------------------ Navigation

    [Fact]
    public void La_section_par_defaut_est_Donnees()
    {
        var vm = Nouveau(new ReglagesHistoriqueMemoire());

        Assert.Equal(SectionReglages.Donnees, vm.Section);
        Assert.True(vm.IsDonnees);
        Assert.False(vm.IsHistorique || vm.IsApparence || vm.IsSessions || vm.IsComportement || vm.IsDiagnostic);
        Assert.True(vm.IsSectionDefilante);
    }

    [Fact]
    public void Le_rail_liste_les_six_sections_dans_l_ordre_du_plan()
    {
        var vm = Nouveau(new ReglagesHistoriqueMemoire());

        Assert.Equal(new[] { "Données", "Historique", "Apparence", "Sessions", "Comportement", "Diagnostic" },
                     vm.Entrees.Select(e => e.Libelle));
        Assert.Equal(new[] { "◉", "◷", "◐", "☰", "◈", "▤" }, vm.Entrees.Select(e => e.Glyphe));
        Assert.Equal(Enum.GetValues<SectionReglages>(), vm.Entrees.Select(e => e.Section));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, vm.Entrees.Select(e => e.Numero));
    }

    [Fact]
    public void Changer_de_section_persiste_et_notifie()
    {
        var reglages = new ReglagesHistoriqueMemoire();
        var vm = Nouveau(reglages);
        var notifiees = new List<string?>();
        vm.PropertyChanged += (_, e) => notifiees.Add(e.PropertyName);

        vm.AllerACommand.Execute(SectionReglages.Apparence);

        Assert.Equal(SectionReglages.Apparence, vm.Section);
        Assert.True(vm.IsApparence);
        Assert.False(vm.IsDonnees);
        Assert.Equal(SectionReglages.Apparence, reglages.Courant.ReglagesSection);
        Assert.Contains(nameof(ReglagesViewModel.IsApparence), notifiees);
        Assert.Contains(nameof(ReglagesViewModel.IsDonnees), notifiees);
    }

    [Fact]
    public void La_derniere_section_est_relue_a_la_construction_et_a_l_ouverture()
    {
        var reglages = new ReglagesHistoriqueMemoire(new ChronosSettings { ReglagesSection = SectionReglages.Comportement });
        var vm = Nouveau(reglages);
        Assert.Equal(SectionReglages.Comportement, vm.Section);

        // Écrite ailleurs entre deux ouvertures (le VM est un singleton qui survit à la fenêtre) : Ouvrir() la relit.
        reglages.Modifier(s => s with { ReglagesSection = SectionReglages.Sessions });
        vm.Ouvrir();
        Assert.Equal(SectionReglages.Sessions, vm.Section);
        Assert.True(vm.IsSessions);
    }

    [Fact]
    public void Ctrl_n_mene_a_la_n_ieme_section_et_ignore_les_autres_chiffres()
    {
        var vm = Nouveau(new ReglagesHistoriqueMemoire());
        var attendu = new[]
        {
            SectionReglages.Donnees, SectionReglages.Historique, SectionReglages.Apparence,
            SectionReglages.Sessions, SectionReglages.Comportement, SectionReglages.Diagnostic,
        };

        for (var n = 6; n >= 1; n--)
        {
            vm.AllerAuNumeroCommand.Execute(n);
            Assert.Equal(attendu[n - 1], vm.Section);
        }

        vm.AllerAuNumeroCommand.Execute(0);
        vm.AllerAuNumeroCommand.Execute(7);
        Assert.Equal(SectionReglages.Donnees, vm.Section);
    }

    [Fact]
    public void La_geometrie_est_memorisee_et_relue()
    {
        var reglages = new ReglagesHistoriqueMemoire();
        var vm = Nouveau(reglages);
        Assert.Equal((null, null, null, null), vm.GeometriePersistee());

        vm.EnregistrerGeometrie(120, 80, 900, 600);

        Assert.Equal((120d, 80d, 900d, 600d), vm.GeometriePersistee());
        Assert.Equal(900, reglages.Courant.ReglagesWidth);
        Assert.Equal(600, reglages.Courant.ReglagesHeight);
    }

    // ------------------------------------------------------------------ Diagnostic

    [Fact]
    public async Task Entrer_dans_Diagnostic_lance_la_generation_une_seule_fois()
    {
        var tcs = new TaskCompletionSource<string>();
        var appels = 0;
        var vm = Nouveau(new ReglagesHistoriqueMemoire(), () => { appels++; return tcs.Task; });
        Assert.Equal(EtatDiagnostic.Vide, vm.EtatDiagnostic);

        vm.AllerACommand.Execute(SectionReglages.Diagnostic);

        Assert.Equal(1, appels);
        Assert.Equal(EtatDiagnostic.EnCours, vm.EtatDiagnostic);
        Assert.True(vm.DiagnosticEnCours);
        Assert.False(vm.IsSectionDefilante);

        tcs.SetResult("=== Chronos — Diagnostic ===\nDate : 2026-09-27\nVersion : 3.4.0\n");
        await vm.ActualiserDiagnosticCommand.ExecutionTask!;

        Assert.Equal(EtatDiagnostic.Pret, vm.EtatDiagnostic);
        Assert.StartsWith("=== Chronos — Diagnostic ===", vm.TexteDiagnostic);
        Assert.Equal("Généré à 21:04 · 3 lignes", vm.LigneDiagnostic);

        // Revenir sur la section ne régénère pas : le rapport en main dit son heure ; « ↻ Actualiser » est le geste.
        vm.AllerACommand.Execute(SectionReglages.Donnees);
        vm.AllerACommand.Execute(SectionReglages.Diagnostic);
        Assert.Equal(1, appels);
    }

    [Fact]
    public async Task Rouvrir_sur_Diagnostic_sans_rapport_lance_la_generation()
    {
        var appels = 0;
        var reglages = new ReglagesHistoriqueMemoire(new ChronosSettings { ReglagesSection = SectionReglages.Diagnostic });
        var vm = Nouveau(reglages, () => { appels++; return Task.FromResult("ligne unique"); });

        vm.Ouvrir();
        await vm.ActualiserDiagnosticCommand.ExecutionTask!;

        Assert.Equal(1, appels);
        Assert.Equal(EtatDiagnostic.Pret, vm.EtatDiagnostic);
        Assert.Equal("Généré à 21:04 · 1 ligne", vm.LigneDiagnostic);
    }

    [Fact]
    public async Task Un_echec_devient_une_phrase_puis_Reessayer_reussit()
    {
        var echoue = true;
        var vm = Nouveau(new ReglagesHistoriqueMemoire(), async () =>
        {
            await Task.Yield();
            if (echoue) throw new InvalidOperationException("coffre illisible");
            return "a\nb";
        });

        await vm.ActualiserDiagnosticCommand.ExecuteAsync(null);

        Assert.Equal(EtatDiagnostic.Echec, vm.EtatDiagnostic);
        Assert.True(vm.DiagnosticEnEchec);
        Assert.Equal("Le diagnostic a échoué : coffre illisible", vm.ErreurDiagnostic);
        Assert.False(vm.CopierDiagnosticCommand.CanExecute(null));

        echoue = false;
        await vm.ActualiserDiagnosticCommand.ExecuteAsync(null);

        Assert.Equal(EtatDiagnostic.Pret, vm.EtatDiagnostic);
        Assert.Equal("", vm.ErreurDiagnostic);
        Assert.Equal("a\nb", vm.TexteDiagnostic);
    }

    [Fact]
    public async Task Copier_met_le_rapport_entier_dans_le_presse_papiers()
    {
        var presse = new FakePressePapiers();
        var vm = Nouveau(new ReglagesHistoriqueMemoire(), () => Task.FromResult("rapport\nentier"), presse);
        Assert.False(vm.CopierDiagnosticCommand.CanExecute(null));   // rien à copier avant un rapport

        await vm.ActualiserDiagnosticCommand.ExecuteAsync(null);
        Assert.True(vm.CopierDiagnosticCommand.CanExecute(null));
        vm.CopierDiagnosticCommand.Execute(null);

        Assert.Equal("rapport\nentier", presse.Dernier);
        Assert.Equal(ReglagesViewModel.TexteCopie, vm.RetourCopie);

        presse.Indisponible = true;
        vm.CopierDiagnosticCommand.Execute(null);
        Assert.Equal(ReglagesViewModel.TexteCopieImpossible, vm.RetourCopie);
    }
}
