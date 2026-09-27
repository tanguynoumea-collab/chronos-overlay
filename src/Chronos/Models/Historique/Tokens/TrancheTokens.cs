namespace Chronos.Models.Historique.Tokens;

/// <summary>
/// TOK-01 — UNE tranche d'agrégat : (début de tranche 15 min UTC, modèle tel que lu, origine sous-agent) → quatre
/// compteurs SÉPARÉS (jamais leur somme : les limites pondèrent par modèle et par nature, une somme n'a aucun sens)
/// et le nombre de messages distincts.
///
/// <para>ENTIERS SEULEMENT : la garde TOK-05 (<c>GardeTokensSansPourcentageTests</c>) rougit sur tout flottant dans
/// ce namespace — ces tokens ne deviennent jamais un pourcentage. Périmètre : Claude Code seulement (transcripts
/// locaux), hors Cowork et claude.ai, bruts et non pondérés (<c>LigneAgregat.Perimetre</c>).</para>
///
/// <para>Le <see cref="Slot"/> est TOUJOURS porté avec un offset zéro (D-33-02) : c'est la clé du fichier mensuel
/// (<c>tokens-AAAA-MM.jsonl</c>, mois UTC du slot) et de l'ordre d'écriture déterministe (D-33-04). Type NEUTRE.</para>
/// </summary>
/// <param name="Slot">Début de la tranche de 15 min, UTC, offset zéro (voir <see cref="SlotDe"/>).</param>
/// <param name="Model">Identifiant du modèle tel que lu dans le transcript (« claude-opus-5 »…), jamais normalisé.</param>
/// <param name="Sub">true = le message vient d'un fichier sous <c>subagents/</c>.</param>
/// <param name="In">Tokens d'entrée (cumul de la tranche).</param>
/// <param name="Out">Tokens de sortie (cumul de la tranche).</param>
/// <param name="CacheW">Tokens d'écriture de cache (cumul de la tranche).</param>
/// <param name="CacheR">Tokens de lecture de cache (cumul de la tranche).</param>
/// <param name="N">Nombre de messages DISTINCTS (un <c>message.id</c> compté une seule fois).</param>
public sealed record TrancheTokens(DateTimeOffset Slot, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, int N)
{
    /// <summary>Largeur d'une tranche. Quinze minutes : la vue Jour (phase 34) montre des barres par quart d'heure.</summary>
    public static readonly TimeSpan Tranche = TimeSpan.FromMinutes(15);

    /// <summary>Début de la tranche de 15 min qui contient <paramref name="t"/>, en UTC, offset zéro.
    /// 01:30 +02:00 le 1er octobre → 23:30Z le 30 septembre (le mois UTC est septembre).</summary>
    public static DateTimeOffset SlotDe(DateTimeOffset t)
    {
        var u = t.UtcDateTime;
        return new DateTimeOffset(u.Year, u.Month, u.Day, u.Hour, u.Minute - u.Minute % 15, 0, TimeSpan.Zero);
    }

    /// <summary>Premier jour du mois UTC qui contient <paramref name="t"/>, 00:00, offset zéro — la clé d'un fichier mensuel.</summary>
    public static DateTimeOffset MoisDe(DateTimeOffset t)
    {
        var u = t.UtcDateTime;
        return new DateTimeOffset(u.Year, u.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }
}

/// <summary>
/// Ce qu'un message (re)rencontré AJOUTE à sa tranche : les quatre compteurs en plus (0 partout si rien de neuf) et
/// « premier passage » (<see cref="NouveauMessage"/> : N n'augmente qu'à la première vue d'un <c>message.id</c>).
/// Produit par la reconstruction (33-03) à partir de l'index d'ids (33-02) — un id déjà connu donne un delta
/// « max par champ − ancien », souvent nul —, consommé par <c>MagasinAgregats.Appliquer</c>.
/// Entiers seulement (garde TOK-05). Type NEUTRE.
/// </summary>
/// <param name="Slot">Tranche cible (offset zéro attendu ; <c>Appliquer</c> normalise par <see cref="TrancheTokens.SlotDe"/>).</param>
/// <param name="Model">Identifiant du modèle tel que lu.</param>
/// <param name="Sub">Origine sous-agent.</param>
/// <param name="In">Entrée en plus.</param>
/// <param name="Out">Sortie en plus.</param>
/// <param name="CacheW">Écriture de cache en plus.</param>
/// <param name="CacheR">Lecture de cache en plus.</param>
/// <param name="NouveauMessage">true = premier passage de cet id : N += 1.</param>
public sealed record DeltaTranche(DateTimeOffset Slot, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, bool NouveauMessage)
{
    /// <summary>Rien à ajouter : ni compteur, ni nouveau message. Un delta nul ne salit pas le mois.</summary>
    public bool EstNul => In == 0 && Out == 0 && CacheW == 0 && CacheR == 0 && !NouveauMessage;
}
