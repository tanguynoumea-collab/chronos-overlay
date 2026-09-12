using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DAT-06 : le composite selectionne la MEILLEURE source PAR FENETRE
/// (Exact prioritaire, sinon Estimated, sinon Unavailable). Deux fakes IUsageProvider renvoient
/// des snapshots fabriques. Tests PURS -> [Fact] classiques.
/// </summary>
public class CompositeUsageProviderTests
{
    // Fake provider retournant un snapshot preconfigure.
    private sealed class FakeProvider : IUsageProvider
    {
        private readonly UsageSnapshot _snap;
        public FakeProvider(UsageSnapshot snap) => _snap = snap;
        public Task<UsageSnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(_snap);
    }

    private static WindowState Win(WindowKind k, SourceReliability r)
        => new() { Kind = k, Reliability = r };

    private static UsageSnapshot Snap(WindowState five, WindowState seven)
        => new() { FiveHour = five, SevenDay = seven };

    // --- Cas 1 : primaire Exact (5 h) + Unavailable (7 j) ; repli Estimated -> 5 h primaire, 7 j repli ---

    [Fact]
    public async Task Prend_primaire_exact_et_repli_estimated_par_fenetre()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Exact);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(pFive, snap.FiveHour);   // primaire Exact conserve
        Assert.Same(fSeven, snap.SevenDay);  // primaire Unavailable -> bascule sur repli Estimated
    }

    // --- Cas 2 : primaire tout Unavailable ; repli tout Estimated -> les deux Estimated (repli) ---

    [Fact]
    public async Task Primaire_indisponible_bascule_entierement_sur_repli()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Unavailable);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(fFive, snap.FiveHour);
        Assert.Same(fSeven, snap.SevenDay);
    }

    // --- Cas 3 : primaire tout Exact ; repli tout Estimated -> primaire prioritaire ---

    [Fact]
    public async Task Primaire_exact_prioritaire_sur_repli()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Exact);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Exact);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(pFive, snap.FiveHour);
        Assert.Same(pSeven, snap.SevenDay);
    }

    // --- Cas 4 : primaire ET repli Unavailable pour une fenetre -> Unavailable, pas de crash ---

    [Fact]
    public async Task Deux_sources_indisponibles_reste_unavailable_sans_crash()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Unavailable);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Unavailable);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- Cas 5 (fix staleness) : tout vient du repli -> SourceCapturedAt = celui du REPLI (frais),
    // pas celui d'un usage.json perime. Un fichier pont vieux ne doit pas marquer « donnees perimees »
    // une estimation JSONL calculee a l'instant. ---

    [Fact]
    public async Task Tout_en_repli_prend_le_capturedAt_du_repli_pas_du_primaire_perime()
    {
        var vieux = new DateTimeOffset(2026, 7, 8, 18, 0, 0, TimeSpan.Zero);   // usage.json d'hier
        var frais = new DateTimeOffset(2026, 7, 9, 8, 0, 0, TimeSpan.Zero);    // scan JSONL a l'instant

        var primaire = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Unavailable),
            Win(WindowKind.SevenDay, SourceReliability.Unavailable)) with { SourceCapturedAt = vieux };
        var repli = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Estimated),
            Win(WindowKind.SevenDay, SourceReliability.Estimated)) with { SourceCapturedAt = frais };

        var composite = new CompositeUsageProvider(new FakeProvider(primaire), new FakeProvider(repli));
        var snap = await composite.GetAsync();

        Assert.Equal(frais, snap.SourceCapturedAt);
    }

    // --- Cas 6 (fix staleness) : au moins une fenetre Exact du primaire -> capturedAt du primaire prime ---

    [Fact]
    public async Task Fenetre_exacte_du_primaire_conserve_le_capturedAt_du_primaire()
    {
        var tPrimaire = new DateTimeOffset(2026, 7, 9, 7, 0, 0, TimeSpan.Zero);
        var tRepli = new DateTimeOffset(2026, 7, 9, 8, 0, 0, TimeSpan.Zero);

        var primaire = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact),
            Win(WindowKind.SevenDay, SourceReliability.Unavailable)) with { SourceCapturedAt = tPrimaire };
        var repli = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Estimated),
            Win(WindowKind.SevenDay, SourceReliability.Estimated)) with { SourceCapturedAt = tRepli };

        var composite = new CompositeUsageProvider(new FakeProvider(primaire), new FakeProvider(repli));
        var snap = await composite.GetAsync();

        Assert.Equal(tPrimaire, snap.SourceCapturedAt);
    }

    // --- Cas 7 (INT-01) : chaîne imbriquée à 3 (OAuth gated → statusLine → JSONL) prime PAR FENÊTRE ---

    [Fact]
    public async Task Chaine_imbriquee_OAuth_prime_puis_statusLine_puis_JSONL_par_fenetre()
    {
        // OAuth : 5h Exact, 7j Unavailable ; statusLine : 5h Unavailable, 7j Exact ; JSONL : tout Estimated.
        var oauth      = Snap(Win(WindowKind.FiveHour, SourceReliability.Exact),
                              Win(WindowKind.SevenDay, SourceReliability.Unavailable));
        var statusLine = Snap(Win(WindowKind.FiveHour, SourceReliability.Unavailable),
                              Win(WindowKind.SevenDay, SourceReliability.Exact));
        var jsonl      = Snap(Win(WindowKind.FiveHour, SourceReliability.Estimated),
                              Win(WindowKind.SevenDay, SourceReliability.Estimated));

        var chaine = new CompositeUsageProvider(
            new FakeProvider(oauth),
            new CompositeUsageProvider(new FakeProvider(statusLine), new FakeProvider(jsonl)));

        var snap = await chaine.GetAsync();

        Assert.Same(oauth.FiveHour, snap.FiveHour);       // 5h : OAuth Exact prime
        Assert.Same(statusLine.SevenDay, snap.SevenDay);  // 7j : OAuth indispo → bascule statusLine Exact
    }

    [Fact]
    public async Task Chaine_imbriquee_OAuth_indispo_bascule_sur_JSONL_estime()
    {
        var oauth      = Snap(Win(WindowKind.FiveHour, SourceReliability.Unavailable),
                              Win(WindowKind.SevenDay, SourceReliability.Unavailable));
        var statusLine = Snap(Win(WindowKind.FiveHour, SourceReliability.Unavailable),
                              Win(WindowKind.SevenDay, SourceReliability.Unavailable));
        var jsonl      = Snap(Win(WindowKind.FiveHour, SourceReliability.Estimated),
                              Win(WindowKind.SevenDay, SourceReliability.Estimated));

        var chaine = new CompositeUsageProvider(
            new FakeProvider(oauth),
            new CompositeUsageProvider(new FakeProvider(statusLine), new FakeProvider(jsonl)));

        var snap = await chaine.GetAsync();
        Assert.Same(jsonl.FiveHour, snap.FiveHour);       // repli ultime : JSONL estimé
        Assert.Same(jsonl.SevenDay, snap.SevenDay);
    }
    // --- HDR-03/HDR-04 : ce que WindowState emporte à travers Best() (phase 18) ---
    //
    // Ces tests prouvent une propriété du code EXISTANT, jamais une fonctionnalité neuve :
    // CompositeUsageProvider.GetAsync reconstruit le snapshot par « new UsageSnapshot { … } » et non par
    // « with », mais Best() rend l'INSTANCE gagnante de WindowState. Un champ posé sur WindowState voyage
    // donc gratuitement, là où un champ posé sur UsageSnapshot serait silencieusement détruit. C'est
    // toute la justification de la conception de la phase 18 — et CompositeUsageProvider.cs reste
    // interdit d'édition jusqu'à la phase 19.

    private static WindowState WinStatut(WindowKind k, SourceReliability r,
                                        StatutServeur? statut = null, EtatDepassement? dep = null)
        => new() { Kind = k, Reliability = r, StatutServeur = statut, Depassement = dep };

    [Fact]
    public async Task Le_statut_serveur_voyage_avec_la_fenetre_gagnante()
    {
        var pFive = WinStatut(WindowKind.FiveHour, SourceReliability.Exact, StatutServeur.AutoriseAvertissement);
        var pSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Exact);
        var fFive = WinStatut(WindowKind.FiveHour, SourceReliability.Exact);   // même fiabilité : le primaire prime
        var fSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Exact);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(pFive, snap.FiveHour);   // transmission PAR RÉFÉRENCE : la raison pour laquelle le champ survit
        Assert.Equal(StatutServeur.AutoriseAvertissement, snap.FiveHour.StatutServeur);
    }

    [Fact]
    public async Task Le_statut_de_la_fenetre_ecartee_ne_contamine_pas_la_gagnante()
    {
        // Le primaire a un statut mais rien d'exploitable : sa fenêtre est écartée, son statut avec elle.
        // Aucune fusion, aucune récupération partielle — Best() choisit une INSTANCE, pas des champs.
        var pFive = WinStatut(WindowKind.FiveHour, SourceReliability.Unavailable, StatutServeur.Rejete);
        var pSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Unavailable, StatutServeur.Rejete);
        var fFive = WinStatut(WindowKind.FiveHour, SourceReliability.Exact);
        var fSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Exact);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(fFive, snap.FiveHour);
        Assert.Null(snap.FiveHour.StatutServeur);
    }

    [Fact]
    public async Task Le_statut_du_repli_remonte_quand_le_repli_gagne()
    {
        var pFive = WinStatut(WindowKind.FiveHour, SourceReliability.Unavailable);
        var pSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = WinStatut(WindowKind.FiveHour, SourceReliability.Exact, StatutServeur.Rejete);
        var fSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Exact, StatutServeur.Rejete);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(fFive, snap.FiveHour);
        Assert.Equal(StatutServeur.Rejete, snap.FiveHour.StatutServeur);
        Assert.Equal(StatutServeur.Rejete, snap.SevenDay.StatutServeur);
    }

    [Fact]
    public async Task Le_depassement_voyage_avec_la_fenetre_gagnante()
    {
        var dep = new EtatDepassement { Utilization = 0.34, Statut = StatutServeur.AutoriseAvertissement };

        var pFive = WinStatut(WindowKind.FiveHour, SourceReliability.Exact, dep: dep);
        var pSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Exact);
        var fFive = WinStatut(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(dep, snap.FiveHour.Depassement);
        Assert.Equal(0.34, snap.FiveHour.Depassement!.Utilization);
    }

    [Fact]
    public async Task Le_depassement_de_la_fenetre_ecartee_est_JETE_par_le_composite()
    {
        // C'est CE cas qui justifie IEtatServeur : le dépassement porté par la fenêtre perdante est jeté par
        // Best(). Le canal latéral est le seul moyen de le faire survivre sans toucher CompositeUsageProvider.cs.
        //
        // Le scénario n'est pas théorique : c'est la forme « dépassement SEUL » (autre type de compte), où la
        // sonde rend deux fenêtres légitimement Unavailable porteuses du seul fait qu'elle connaisse. Dès
        // qu'une AUTRE source rend un Exact, les deux instances de la sonde sont écartées — et l'information
        // de dépassement disparaît sans bruit.
        var dep = new EtatDepassement { Utilization = 0.34 };

        var sonde = Snap(WinStatut(WindowKind.FiveHour, SourceReliability.Unavailable, dep: dep),
                         WinStatut(WindowKind.SevenDay, SourceReliability.Unavailable, dep: dep));
        var autre = Snap(WinStatut(WindowKind.FiveHour, SourceReliability.Exact),
                         WinStatut(WindowKind.SevenDay, SourceReliability.Exact));

        var composite = new CompositeUsageProvider(new FakeProvider(sonde), new FakeProvider(autre));

        var snap = await composite.GetAsync();

        Assert.Null(snap.FiveHour.Depassement);
        Assert.Null(snap.SevenDay.Depassement);
    }

    [Fact]
    public async Task Le_statut_traverse_DEUX_composites_imbriques()
    {
        // La chaîne réelle après le plan 18-05 : sonde au-dessus de (OAuth au-dessus de JSONL).
        // Le statut doit traverser les deux niveaux sans perte — sinon il n'arriverait jamais à l'UI.
        var sondeFive = WinStatut(WindowKind.FiveHour, SourceReliability.Exact, StatutServeur.Rejete);
        var sondeSeven = WinStatut(WindowKind.SevenDay, SourceReliability.Unavailable, StatutServeur.Rejete);
        var sonde = Snap(sondeFive, sondeSeven);

        var interneHaut = Snap(WinStatut(WindowKind.FiveHour, SourceReliability.Estimated),
                               WinStatut(WindowKind.SevenDay, SourceReliability.Exact,
                                         StatutServeur.AutoriseAvertissement));
        var interneBas = Snap(WinStatut(WindowKind.FiveHour, SourceReliability.Estimated),
                              WinStatut(WindowKind.SevenDay, SourceReliability.Estimated));

        var chaine = new CompositeUsageProvider(
            new FakeProvider(sonde),
            new CompositeUsageProvider(new FakeProvider(interneHaut), new FakeProvider(interneBas)));

        var snap = await chaine.GetAsync();

        // 5 h : la sonde est Exact, elle prime — son statut arrive intact au sommet.
        Assert.Same(sondeFive, snap.FiveHour);
        Assert.Equal(StatutServeur.Rejete, snap.FiveHour.StatutServeur);

        // 7 j : la sonde est indisponible, le composite INTERNE gagne — et le statut qu'il porte remonte
        // lui aussi les deux niveaux.
        Assert.Same(interneHaut.SevenDay, snap.SevenDay);
        Assert.Equal(StatutServeur.AutoriseAvertissement, snap.SevenDay.StatutServeur);
    }
}
