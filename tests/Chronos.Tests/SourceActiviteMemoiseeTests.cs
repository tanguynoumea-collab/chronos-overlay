using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la contrainte de COÛT de la phase 19 : une passe de transcripts coûte 2,7 à 3,2 s et lit
/// 536 Mo sur la machine cible. Au tick de 60 s, une lecture inconditionnelle est exclue — le
/// mémoïseur existe pour cela, et le compteur de passes du faux est l'instrument de la preuve.
///
/// Prouve aussi les deux décisions d'HONNÊTETÉ du décorateur : la validité se mesure sur l'instant de
/// la PASSE DISQUE (un journal né vieux expire vite) et une panne de la source ne rend jamais un journal
/// périmé (DS2-02, D-02) : un journal encore valide est servi sans relecture, mais hors de sa validité il
/// n'est JAMAIS rendu — l'exception remonte et la tête en fait « Indisponible », jamais « exact ». Et,
/// quand on ne savait rien, le décorateur n'invente pas de journal.
///
/// Tests PURS (aucun type WPF) → [Fact] classiques, horloge injectée. Seul le test de composition
/// doctrinale touche le disque, dans un dossier unique sous Path.GetTempPath() (vérifié, nettoyé).
/// </summary>
public class SourceActiviteMemoiseeTests
{
    private static readonly DateTimeOffset Depart = new(2026, 09, 12, 12, 0, 0, TimeSpan.Zero);

    // Journal vide daté de « now » : seul son instant de passe disque compte pour la péremption.
    private static TranscriptActivityLog Journal(DateTimeOffset now)
        => new(now, now - TimeSpan.FromDays(8), Array.Empty<(DateTimeOffset, long)>());

    [Fact]
    public async Task Deux_lectures_rapprochees_ne_paient_QU_UNE_passe_disque()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        var j1 = await memo.ReadAsync();
        var j2 = await memo.ReadAsync();

        Assert.Equal(1, faux.Lectures);   // 536 Mo lus UNE fois, pas deux
        Assert.Same(j1, j2);              // le journal est PUR : le réutiliser est sans perte
    }

    [Fact]
    public async Task Une_lecture_apres_peremption_repaie_une_passe()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        await memo.ReadAsync();

        horloge.UtcNow = Depart.AddSeconds(61);           // au-delà de la validité
        faux.Journal = Journal(horloge.UtcNow);
        var frais = await memo.ReadAsync();

        Assert.Equal(2, faux.Lectures);
        Assert.Equal(Depart.AddSeconds(61), frais.Now);
    }

    [Fact]
    public async Task La_validite_se_mesure_sur_l_instant_de_la_passe_disque()
    {
        var horloge = new FakeClock(Depart);
        // Journal NÉ VIEUX : sa passe disque date de 120 s, alors qu'on vient tout juste de l'obtenir.
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart.AddSeconds(-120)) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        await memo.ReadAsync();
        await memo.ReadAsync();   // l'horloge n'a PAS bougé, et pourtant il faut repayer

        Assert.Equal(2, faux.Lectures);
    }

    // DS2-02 / D-02 : remplace « Une_panne_de_la_source_rend_le_dernier_journal_connu », qui ÉPINGLAIT le
    // défaut — un journal hors de sa validité rendu quel que soit son âge, donc un relevé vieilli resté
    // « exact — encore valide » alors que Claude Code avait pu travailler depuis.
    [Fact]
    public async Task Une_panne_apres_peremption_remonte_et_ne_rend_JAMAIS_le_journal_perime()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        await memo.ReadAsync();

        faux.Journal = null;                      // le disque tombe
        horloge.UtcNow = Depart.AddSeconds(61);   // et la validité expire

        await Assert.ThrowsAsync<IOException>(() => memo.ReadAsync());
        Assert.Equal(2, faux.Lectures);           // la tentative A BIEN eu lieu
    }

    [Fact]
    public async Task Un_journal_encore_valide_est_servi_sans_relecture_meme_source_en_panne()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        var connu = await memo.ReadAsync();

        faux.Journal = null;                      // la source tombe…
        horloge.UtcNow = Depart.AddSeconds(30);   // …mais le journal est encore dans sa validité

        var servi = await memo.ReadAsync();       // chemin rapide : aucune relecture, aucune exception

        Assert.Same(connu, servi);
        Assert.Equal(1, faux.Lectures);
    }

    [Fact]
    public async Task Apres_une_panne_la_source_retablie_redonne_un_journal_frais()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        await memo.ReadAsync();
        faux.Journal = null;
        horloge.UtcNow = Depart.AddSeconds(61);
        await Assert.ThrowsAsync<IOException>(() => memo.ReadAsync());

        horloge.UtcNow = Depart.AddSeconds(90);
        faux.Journal = Journal(Depart.AddSeconds(90));   // la source revient

        var frais = await memo.ReadAsync();

        Assert.Equal(Depart.AddSeconds(90), frais.Now);
        Assert.Equal(3, faux.Lectures);
    }

    // DS2-02 / D-02, preuve par COMPOSITION RÉELLE : mémoïseur + tête (LastExactUsageProvider) + magasin
    // temporaire + doctrine. Un journal périmé et une source en panne ne certifient jamais un relevé vieilli.
    [Fact]
    public async Task Via_la_doctrine_un_journal_perime_ne_certifie_jamais_un_releve_exact()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ChronosMemoDoctrine_" + Guid.NewGuid().ToString("N"));
        Assert.StartsWith(Path.GetTempPath(), dossier);
        Directory.CreateDirectory(dossier);
        try
        {
            var horloge = new FakeClock(Depart);
            var faux = new FakeTranscriptActivitySource { Journal = Journal(Depart) };   // aucune activité
            var memo = new SourceActiviteMemoisee(faux, horloge);
            var store = new LastExactStore(Path.Combine(dossier, "last-exact.json"));

            var capture = Depart.AddHours(-2);                    // relevé vieilli : au-delà de LimiteAge
            var inner = new FakeUsageProvider
            {
                Next = new UsageSnapshot
                {
                    FiveHour = new WindowState
                    {
                        Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                        Utilization = 0.42, ResetsAt = Depart.AddHours(3), CapturedAt = capture,
                    },
                    SevenDay = new WindowState
                    {
                        Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact,
                        Utilization = 0.30, ResetsAt = Depart.AddDays(4), CapturedAt = capture,
                    },
                },
            };
            var tete = new LastExactUsageProvider(inner, store, horloge, memo);

            // Témoin : journal frais sans activité depuis la capture → « exact — encore valide ».
            var temoin = await tete.GetAsync();
            Assert.Equal(SourceReliability.Exact, temoin.FiveHour.Reliability);
            Assert.Equal(ProvenanceReleve.EncoreValide, temoin.FiveHour.Provenance);
            Assert.Equal(ProvenanceReleve.EncoreValide, temoin.SevenDay.Provenance);

            // La source tombe et le journal sort de sa validité : plus RIEN ne fonde « encore valide ».
            faux.Journal = null;
            horloge.UtcNow = Depart.AddSeconds(61);

            var snap = await tete.GetAsync();
            foreach (var w in new[] { snap.FiveHour, snap.SevenDay })
            {
                Assert.Equal(SourceReliability.Unavailable, w.Reliability);
                Assert.Null(w.Utilization);
                Assert.Null(w.Provenance);
            }
        }
        finally
        {
            try { Directory.Delete(dossier, recursive: true); } catch { /* nettoyage best-effort */ }
        }
    }

    [Fact]
    public async Task Une_panne_des_la_premiere_lecture_remonte_a_l_appelant()
    {
        var horloge = new FakeClock(Depart);
        var faux = new FakeTranscriptActivitySource { Journal = null };
        var memo = new SourceActiviteMemoisee(faux, horloge, TimeSpan.FromSeconds(60));

        // Rien de connu : le décorateur n'invente PAS un journal vide — un journal vide se lirait
        // « aucune activité », ce qui est une AFFIRMATION. C'est l'appelant (plan 19-03) qui
        // convertira cela en branche « indisponible ».
        await Assert.ThrowsAsync<IOException>(() => memo.ReadAsync());
    }

    [Fact]
    public void La_validite_par_defaut_est_alignee_sur_le_tick_nominal()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), SourceActiviteMemoisee.ValiditeParDefaut);
    }
}
