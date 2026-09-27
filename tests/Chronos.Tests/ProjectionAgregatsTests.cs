using Chronos.Models.Historique.Tokens;
using Chronos.Services.Historique.Tokens;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// TOK-02 — la projection : l'index d'ids (mémoire d'idempotence, D-33-07) redevient des tranches (slot 15 min UTC,
/// modèle, origine) sommées, avec N = nombre d'ids. Pur, sans E/S : c'est ce que la reconstruction rejoue au démarrage
/// sur les mois ouverts pour que le fichier d'agrégats ne soit jamais autre chose que la projection de l'index.
/// </summary>
public sealed class ProjectionAgregatsTests
{
    private static DateTimeOffset Utc(string iso)
        => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static (DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR) E(
        string iso, string model, bool sub, long entree, long sortie, long cacheW, long cacheR)
        => (Utc(iso), model, sub, entree, sortie, cacheW, cacheR);

    [Fact]
    public void Projeter_groupe_par_slot_modele_et_origine_et_compte_les_ids()
    {
        var entrees = new[]
        {
            E("2026-09-21T14:00:00Z", "claude-opus-5", false, 1, 10, 100, 1000),
            E("2026-09-21T14:01:00Z", "claude-opus-5", false, 2, 20, 200, 2000),
            E("2026-09-21T14:02:00Z", "claude-opus-5", false, 3, 30, 300, 3000),
            E("2026-09-21T14:20:00Z", "claude-opus-5", false, 4, 40, 400, 4000),
            E("2026-09-21T14:21:00Z", "claude-opus-5", false, 5, 50, 500, 5000),
            E("2026-09-21T14:05:00Z", "claude-sonnet-5", true, 1, 1, 1, 1),
        };

        var tranches = ProjectionAgregats.Projeter(entrees);

        Assert.Equal(new[]
        {
            new TrancheTokens(Utc("2026-09-21T14:00:00Z"), "claude-opus-5", false, 6, 60, 600, 6000, 3),
            new TrancheTokens(Utc("2026-09-21T14:00:00Z"), "claude-sonnet-5", true, 1, 1, 1, 1, 1),
            new TrancheTokens(Utc("2026-09-21T14:15:00Z"), "claude-opus-5", false, 9, 90, 900, 9000, 2),
        }, tranches);
    }

    [Fact]
    public void Le_slot_vient_du_premier_timestamp_de_l_id()
    {
        // L'index a déjà fusionné les blocs d'un même id (le max par champ) : une entrée = un id, et son Ts est le PREMIER vu.
        var tranches = ProjectionAgregats.Projeter(new[] { E("2026-09-21T10:59:58Z", "claude-opus-5", false, 7, 256, 70, 700) });

        var t = Assert.Single(tranches);
        Assert.Equal(Utc("2026-09-21T10:45:00Z"), t.Slot);
        Assert.Equal(TimeSpan.Zero, t.Slot.Offset);
        Assert.Equal(1, t.N);
        Assert.Equal((7L, 256L, 70L, 700L), (t.In, t.Out, t.CacheW, t.CacheR));
    }

    [Fact]
    public void Une_liste_vide_donne_une_projection_vide()
    {
        var tranches = ProjectionAgregats.Projeter(Array.Empty<(DateTimeOffset, string, bool, long, long, long, long)>());

        Assert.Empty(tranches);
    }
}
