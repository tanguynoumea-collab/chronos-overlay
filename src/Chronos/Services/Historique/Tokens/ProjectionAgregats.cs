using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-02 — l'agrégat est une PROJECTION de l'index d'ids (D-33-07) : ce type la calcule. Pur, sans E/S, sans horloge.
///
/// <para>POURQUOI : le fichier <c>tokens-AAAA-MM.jsonl</c> d'un mois ouvert n'est jamais la vérité — l'index d'ids l'est.
/// Au démarrage, la reconstruction reprojette chaque mois ouvert depuis l'index et REMPLACE le fichier ; ainsi une panne
/// entre l'écriture des ids et celle des agrégats (ordre de flush D-33-14) ne perd rien : les ids écrits sont reprojetés,
/// les autres seront relus depuis le dernier curseur persisté.</para>
///
/// <para>Une entrée = un id (l'index a déjà fusionné les blocs par le max) ; son <c>Ts</c> est le PREMIER vu, d'où le slot.
/// N compte les ids. Tri (slot, modèle ordinal, origine) : le même que le magasin, pour des octets identiques (D-33-04).</para>
/// </summary>
public static class ProjectionAgregats
{
    public static IReadOnlyList<TrancheTokens> Projeter(
        IEnumerable<(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR)> entrees)
        => entrees
            .GroupBy(e => (Slot: TrancheTokens.SlotDe(e.Ts), e.Model, e.Sub))
            .Select(g => new TrancheTokens(
                g.Key.Slot, g.Key.Model, g.Key.Sub,
                g.Sum(e => e.In), g.Sum(e => e.Out), g.Sum(e => e.CacheW), g.Sum(e => e.CacheR),
                g.Count()))
            .OrderBy(t => t.Slot)
            .ThenBy(t => t.Model, StringComparer.Ordinal)
            .ThenBy(t => t.Sub)
            .ToList();
}
