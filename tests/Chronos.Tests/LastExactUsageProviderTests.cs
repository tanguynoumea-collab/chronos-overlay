using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve EXA-01 côté décorateur : il est l'ÉCRIVAIN UNIQUE du magasin (il voit le snapshot fusionné
/// qui sera affiché), et — depuis la phase 19 — il est LA COUCHE DE DOCTRINE : c'est lui qui statue sur
/// l'âge de chaque fenêtre, ré-habilite un relevé sans activité depuis (DEL-03), le démote en plancher
/// marqué quand il y a eu de l'activité (DEL-04), et remonte le bit EXA-05 jusqu'au snapshot.
///
/// Ce n'est plus le rebouchage de trou de la phase 16 : une fenêtre vivante peut désormais être démotée,
/// et une fenêtre mémorisée servie qualifiée — mais jamais gonflée d'un delta (EXA-04).
///
/// Isolation stricte : magasin dans un dossier temp unique — jamais %APPDATA%\Chronos.
/// Tests purs (aucun type WPF) → [Fact] classiques, pas de STA.
/// </summary>
public class LastExactUsageProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 09, 09, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir;
    private readonly string _fichier;
    private readonly LastExactStore _store;
    private readonly FakeClock _clock = new(Now);
    private readonly FakeTranscriptActivitySource _activite = new();

    public LastExactUsageProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosLastExactDeco_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _fichier = Path.Combine(_dir, "last-exact.json");
        _store = new LastExactStore(_fichier);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private static WindowState Win(WindowKind k, SourceReliability r, double? util = null,
                                   DateTimeOffset? resets = null, DateTimeOffset? captured = null)
        => new()
        {
            Kind = k,
            Utilization = util,
            ResetsAt = resets,
            CapturedAt = captured,
            Reliability = r,
        };

    private static UsageSnapshot Snap(WindowState five, WindowState seven)
        => new() { FiveHour = five, SevenDay = seven };

    /// <summary>Journal d'activité programmable. Horizon à huit jours : les relevés de quelques heures
    /// sont donc TOUJOURS couverts, et l'échec par horizon (branche 4a) reste testé là où il vit,
    /// dans DoctrineFraicheurTests.</summary>
    private static TranscriptActivityLog Journal(params (DateTimeOffset Ts, long Tokens)[] e)
        => new(Now, Now - TimeSpan.FromDays(8), e);

    private LastExactUsageProvider Deco(FakeUsageProvider inner)
        => new(inner, _store, _clock, _activite);

    private LastExactUsageProvider Deco(FakeUsageProvider inner, LastExactStore store)
        => new(inner, store, _clock, _activite);

    // --- 1. Écriture : chaque fenêtre exacte vue passer est mémorisée ---

    [Fact]
    public async Task Ecrit_dans_le_magasin_les_deux_fenetres_exactes()
    {
        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), Now),
                Win(WindowKind.SevenDay, SourceReliability.Exact, 0.63, Now.AddDays(4), Now)),
        };

        await Deco(inner).GetAsync();

        var persiste = _store.Load(Now);
        Assert.NotNull(persiste);
        Assert.Equal(0.42, persiste!.FiveHour!.Utilization);
        Assert.Equal(0.63, persiste.SevenDay!.Utilization);
    }

    // --- 2. Passe-plat : quand tout est vivant et frais, le décorateur est transparent ---

    [Fact]
    public async Task Snapshot_entierement_exact_est_retourne_tel_quel()
    {
        var five = Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), Now);
        var seven = Win(WindowKind.SevenDay, SourceReliability.Exact, 0.63, Now.AddDays(4), Now);
        var inner = new FakeUsageProvider { Next = Snap(five, seven) };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(0.42, snap.FiveHour.Utilization);
        Assert.Equal(Now.AddHours(3), snap.FiveHour.ResetsAt);
        Assert.Equal(0.63, snap.SevenDay.Utilization);
        Assert.Equal(Now.AddDays(4), snap.SevenDay.ResetsAt);
    }

    // --- 3a / 3b. LA TRACE EXÉCUTABLE DU CHANGEMENT DE DOCTRINE ---------------------------------
    // Ces deux tests remplacent l'unique « Rebouche_une_fenetre_indisponible_depuis_le_magasin » de la
    // phase 16, qui affirmait qu'un relevé de deux heures est Exact SANS poser aucune question sur
    // l'activité. Il n'est pas supprimé : il est SCINDÉ selon la réponse à cette question, désormais
    // posée. C'est exactement ce que DEL-03 et DEL-04 ajoutent au dépôt.

    [Fact]
    public async Task Un_releve_de_deux_heures_SANS_activite_depuis_reste_EXACT()
    {
        var capture = Now.AddHours(-2);
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), capture),
            WindowState.Unavailable(WindowKind.SevenDay)));

        // La seule entrée du journal est ANTÉRIEURE à la capture : rien depuis, donc l'utilisation
        // n'a pas bougé. Ce n'est pas une indulgence, c'est une déduction (DEL-03).
        _activite.Journal = Journal((capture.AddHours(-1), 5_000));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(ProvenanceReleve.EncoreValide, snap.FiveHour.Provenance);
        Assert.Equal(0.42, snap.FiveHour.Utilization);
        Assert.Equal(capture, snap.FiveHour.CapturedAt);   // horodatage d'ORIGINE conservé
        Assert.Equal(0L, snap.FiveHour.TokensDepuisReleve); // MESURÉ à zéro, et non « non mesuré »
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    [Fact]
    public async Task Un_releve_de_deux_heures_AVEC_activite_depuis_devient_un_plancher_marque()
    {
        var capture = Now.AddHours(-2);
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), capture),
            WindowState.Unavailable(WindowKind.SevenDay)));

        _activite.Journal = Journal((capture.AddMinutes(30), 750_000));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, snap.FiveHour.Provenance);
        // La valeur ne bouge PAS d'un iota : c'est sa NATURE qui change (« au moins 42 % »), EXA-04.
        Assert.Equal(0.42, snap.FiveHour.Utilization);
        Assert.Equal(750_000L, snap.FiveHour.TokensDepuisReleve);
    }

    // --- 4. Une réponse vivante fraîche n'est JAMAIS remplacée par le magasin ---

    [Fact]
    public async Task Ne_remplace_jamais_une_reponse_vivante()
    {
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.80, Now.AddHours(3), Now.AddHours(-2)),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Exact, 0.20, Now.AddHours(4), Now),
                WindowState.Unavailable(WindowKind.SevenDay)),
        };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(0.20, snap.FiveHour.Utilization);   // la valeur vivante, pas le 0,80 du magasin
    }

    // --- 5. Un relevé dont le reset est passé n'est jamais ressuscité ---

    [Fact]
    public async Task Ne_rebouche_pas_depuis_une_fenetre_dont_le_reset_est_passe()
    {
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.80, Now.AddMinutes(-1), Now.AddHours(-6)),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Null(snap.FiveHour.Utilization);
    }

    // --- 6. NON-CONTAMINATION DU MAGASIN : un plancher n'y entre jamais ---------------------------
    // Remplace « Fenetre_estimee_n_est_ni_rebouchee_ni_ecrite », qui injectait un Estimated VIVANT —
    // impossible en production depuis la phase 16, et dont le mot change de sens en phase 19 (Estimated
    // = plancher). La propriété qui compte désormais est l'inverse : le plancher que la doctrine PRODUIT
    // ne doit jamais redescendre dans le magasin, sinon la dégradation deviendrait permanente.

    [Fact]
    public async Task Un_plancher_n_est_JAMAIS_persiste_comme_exact()
    {
        var capture = Now.AddHours(-2);
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.80, Now.AddHours(3), capture),
            WindowState.Unavailable(WindowKind.SevenDay)));
        var avant = File.ReadAllText(_fichier);

        _activite.Journal = Journal((capture.AddMinutes(30), 900_000));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        // Le snapshot sorti EST un plancher…
        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
        Assert.Equal(ProvenanceReleve.PlancherAvecActivite, snap.FiveHour.Provenance);

        // …et le magasin est octet pour octet identique : il ne contient que de l'exact certifié.
        Assert.Equal(avant, File.ReadAllText(_fichier));
    }

    // --- 7. Robustesse : un magasin EN PANNE ne prive de rien et n'affirme rien ------------------
    // Conserve l'intention du test de la phase 16 (robustesse), change l'entrée : un Exact FRAIS, car
    // un Estimated vivant n'existe plus. Et ajoute l'assertion EXA-05 : un magasin qui ne répond pas ne
    // peut pas déclarer « aucun exact n'a jamais été obtenu ».

    [Fact]
    public async Task Un_magasin_en_panne_ne_leve_pas_et_n_affirme_rien_sur_le_passe()
    {
        // Panne DURE et déterministe. Un simple fichier de JSON invalide ne suffirait pas :
        // LastExactStore est tolérant PAR CONSTRUCTION (toute défaillance de lecture rend null sans
        // lever), donc il répondrait « false », c'est-à-dire une AFFIRMATION. Ici son dossier parent
        // est en réalité un FICHIER : il ne peut ni lire, ni écrire, donc ne peut rien affirmer.
        var poison = Path.Combine(_dir, "poison");
        File.WriteAllText(poison, "je suis un fichier, pas un dossier");
        var enPanne = new LastExactStore(Path.Combine(poison, "last-exact.json"));

        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Exact, 0.30, Now.AddHours(3), Now),
                WindowState.Unavailable(WindowKind.SevenDay)),
        };

        var snap = await Deco(inner, enPanne).GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(ProvenanceReleve.Frais, snap.FiveHour.Provenance);
        Assert.Equal(0.30, snap.FiveHour.Utilization);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Null(snap.UnExactADejaEteObtenu);   // « je ne sais pas » ≠ « jamais » (EXA-05)
    }

    // --- 8. Aucune écriture inutile : rien d'exact, rien à mémoriser ---

    [Fact]
    public async Task Deux_passages_sans_rien_d_exact_ne_creent_aucun_fichier()
    {
        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };
        var deco = Deco(inner);

        await deco.GetAsync();
        await deco.GetAsync();

        Assert.False(File.Exists(_fichier));
        Assert.Equal(2, inner.GetCount);
    }

    // --- 9. Aucun horodatage inventé au niveau snapshot : l'âge honnête est porté par la fenêtre ---
    // Intention conservée ; le journal fourni est SANS activité depuis la capture, pour rester en
    // branche 2 — sans lui, la fenêtre serait démotée et l'assertion sur CapturedAt perdrait son objet.

    [Fact]
    public async Task Rebouchage_n_invente_aucun_SourceCapturedAt()
    {
        var capture = Now.AddHours(-2);
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), capture),
            WindowState.Unavailable(WindowKind.SevenDay)));

        _activite.Journal = Journal((capture.AddHours(-1), 5_000));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Null(snap.SourceCapturedAt);              // inconnu reste inconnu
        Assert.Equal(capture, snap.FiveHour.CapturedAt); // l'âge honnête vit sur la fenêtre
    }
}
