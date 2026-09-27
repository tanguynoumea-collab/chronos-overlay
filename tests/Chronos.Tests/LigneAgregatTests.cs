using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-01 — une ligne d'agrégat de tokens s'écrit et se relit sans ambiguïté, et une ligne cassée se saute
/// en étant COMPTÉE.
///
/// Ce que ces tests gravent : les NEUF champs du contrat de données (.zeus/DESIGN_PLAN.md §3) dans un ordre
/// fixe <c>{v, slot, model, sub, in, out, cache_w, cache_r, n}</c> ; les quatre compteurs SÉPARÉS, jamais leur
/// somme ; le <c>slot</c> en UTC au format « O » (D-33-02 : un seul dialecte d'instant dans historique\) et
/// ALIGNÉ sur 15 min (D-33-03 : un slot non aligné est refusé) ; un agrégat sans compteur n'a pas de sens et
/// est refusé (différent du journal des relevés, où un champ absent vaut null) ; <c>sub</c> absent vaut false ;
/// <c>v</c> est lu EN PREMIER ; les entiers partent invariants même sous fr-FR.
///
/// Tests purs : aucune E/S hors de la fixture versionnée, aucun %APPDATA%, aucune horloge système.
/// </summary>
public class LigneAgregatTests
{
    private static readonly DateTimeOffset Slot = new(2026, 09, 01, 0, 0, 0, TimeSpan.Zero);

    private const string Ligne1 =
        "{\"v\":1,\"slot\":\"2026-09-01T00:00:00.0000000+00:00\",\"model\":\"claude-opus-5\",\"sub\":true,"
        + "\"in\":72,\"out\":398,\"cache_w\":48601,\"cache_r\":20376807,\"n\":36}";

    private static string CheminFixture([CallerFilePath] string ceFichier = "")
        => Path.Combine(Path.GetDirectoryName(ceFichier)!, "TestData", "tokens", "tolerance", "tokens-2026-09.jsonl");

    private static string[] LignesFixture() => File.ReadAllText(CheminFixture()).Split('\n');

    private static TrancheTokens TrancheComplete() => new(Slot, "claude-opus-5", true, 72, 398, 48601, 20376807, 36);

    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // --- Sérialisation ---

    [Fact]
    public void Une_tranche_se_serialise_avec_les_neuf_champs_du_contrat_dans_l_ordre()
    {
        var ligne = LigneAgregat.Serialiser(TrancheComplete());

        Assert.Equal(Ligne1, ligne);
        Assert.Equal(LignesFixture()[0], ligne);   // la fixture EST le contrat : ligne 1, sans « \n »
        Assert.DoesNotContain("\n", ligne);
        Assert.DoesNotContain(" ", ligne);
    }

    [Fact]
    public void Le_slot_est_ecrit_en_UTC_au_format_O_quel_que_soit_l_offset_d_entree()
    {
        var aParis = new DateTimeOffset(2026, 09, 01, 2, 0, 0, TimeSpan.FromHours(2));   // = 00:00Z

        var ligne = LigneAgregat.Serialiser(new TrancheTokens(aParis, "claude-opus-5", false, 1, 1, 1, 1, 1));

        Assert.Contains("\"slot\":\"2026-09-01T00:00:00.0000000+00:00\"", ligne);
        Assert.DoesNotContain("+02:00", ligne);
    }

    [Fact]
    public void Serialiser_puis_Parser_rend_la_meme_tranche()
    {
        var origine = TrancheComplete();

        Assert.True(LigneAgregat.Parser(LigneAgregat.Serialiser(origine), out var relue));

        Assert.Equal(origine, relue);
        Assert.Equal(TimeSpan.Zero, relue!.Slot.Offset);
    }

    [Fact]
    public void Aucune_somme_des_quatre_compteurs_n_apparait_dans_la_ligne()
    {
        var ligne = LigneAgregat.Serialiser(TrancheComplete());   // 72 + 398 + 48601 + 20376807 = 20425878

        Assert.DoesNotContain("20425878", ligne);
        Assert.DoesNotContain("total", ligne, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"in\":72", ligne);
        Assert.Contains("\"out\":398", ligne);
        Assert.Contains("\"cache_w\":48601", ligne);
        Assert.Contains("\"cache_r\":20376807", ligne);
    }

    // --- Tolérance ---

    [Fact]
    public void Une_ligne_avec_v_inconnu_slot_non_aligne_ou_compteur_absent_est_refusee()
    {
        var lignes = LignesFixture();

        // Lignes physiques 2 (v = 2), 3 (slot 00:07, non aligné), 4 (out absent), 6 (tronquée), 8 (model absent).
        foreach (var i in new[] { 1, 2, 3, 5, 7 })
        {
            Assert.False(LigneAgregat.Parser(lignes[i], out var tranche), $"La ligne {i + 1} de la fixture devrait être refusée : {lignes[i]}");
            Assert.Null(tranche);
        }

        Assert.False(LigneAgregat.Parser("", out _));
        Assert.False(LigneAgregat.Parser("{", out _));
        Assert.False(LigneAgregat.Parser("[1,2]", out _));

        // Et la fixture entière : 2 tranches valides (lignes 1 et 7), 5 refusées, la ligne vide ni lue ni comptée.
        int acceptees = 0, refusees = 0;
        foreach (var l in lignes)
        {
            if (l.Length == 0) continue;
            if (LigneAgregat.Parser(l, out _)) acceptees++; else refusees++;
        }
        Assert.Equal(2, acceptees);
        Assert.Equal(5, refusees);
    }

    [Fact]
    public void Sub_absent_vaut_false()
    {
        var ligne7 = LignesFixture()[6];

        Assert.True(LigneAgregat.Parser(ligne7, out var tranche));

        Assert.False(tranche!.Sub);
        Assert.Equal("claude-sonnet-5", tranche.Model);
        Assert.Equal(2, tranche.N);
        Assert.Equal(5, tranche.In);
        Assert.Equal(50, tranche.Out);
        Assert.Equal(0, tranche.CacheW);
        Assert.Equal(500, tranche.CacheR);
        Assert.Equal(Utc("2026-09-01T01:00:00Z"), tranche.Slot);
    }

    // --- Contrat ---

    [Fact]
    public void Champs_et_Perimetre_sont_ceux_du_contrat()
    {
        Assert.Equal(new[] { "v", "slot", "model", "sub", "in", "out", "cache_w", "cache_r", "n" }, LigneAgregat.Champs);
        Assert.Equal("Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés", LigneAgregat.Perimetre);
        Assert.Equal(1, LigneAgregat.SchemaVersion);
    }

    [Fact]
    public void SlotDe_tronque_a_la_tranche_de_quinze_minutes_UTC()
    {
        Assert.Equal(Utc("2026-09-21T10:45:00Z"), TrancheTokens.SlotDe(Utc("2026-09-21T10:59:58Z")));
        Assert.Equal(Utc("2026-09-21T11:00:00Z"), TrancheTokens.SlotDe(Utc("2026-09-21T11:00:07Z")));

        // 01:30 +02:00 le 1er octobre = 23:30Z le 30 septembre : le mois UTC est septembre.
        var premierOctobreParis = new DateTimeOffset(2026, 10, 01, 1, 30, 0, TimeSpan.FromHours(2));
        var slot = TrancheTokens.SlotDe(premierOctobreParis);
        Assert.Equal(Utc("2026-09-30T23:30:00Z"), slot);
        Assert.Equal(TimeSpan.Zero, slot.Offset);

        Assert.Equal(Utc("2026-09-01T00:00:00Z"), TrancheTokens.MoisDe(Utc("2026-09-30T23:30:00Z")));
        Assert.Equal(Utc("2026-09-01T00:00:00Z"), TrancheTokens.MoisDe(premierOctobreParis));
    }

    [Fact]
    public void Les_entiers_partent_invariants_sous_fr_FR()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var ligne = LigneAgregat.Serialiser(TrancheComplete());

            Assert.Contains("\"cache_r\":20376807", ligne);
            Assert.DoesNotContain(" ", ligne);   // espace insécable (séparateur de groupes fr-FR)
            Assert.DoesNotContain(" ", ligne);   // espace fine insécable (fr-FR récent)
            Assert.Equal(Ligne1, ligne);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
