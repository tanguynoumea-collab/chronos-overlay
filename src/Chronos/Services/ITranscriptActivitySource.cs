namespace Chronos.Services;

/// <summary>
/// Résultat BORNÉ d'une interrogation des transcripts (DEL-01 + DEL-02). Aucun pourcentage, jamais.
///
/// <para><b>Tokens est une BORNE INFÉRIEURE de l'activité Claude Code, pas une consommation de compte.</b>
/// ~/.claude/projects/**/*.jsonl ne contient que les transcripts de Claude Code : l'usage de l'app de
/// bureau et de Cowork consomme le MÊME pool de compte mais n'y apparaît pas. De plus, les limites
/// Anthropic pondèrent par modèle — une somme de tokens n'est PAS convertible en pourcentage. La marge
/// d'incertitude qui en découle est portée par la phase 19 (DEL-04).</para>
/// </summary>
/// <param name="HasActivity">Vrai s'il existe au moins une réponse assistant dans la fenêtre interrogée.
/// Redondant avec <paramref name="LastActivityAt"/> : c'est ASSUMÉ, DEL-01 demande explicitement une
/// réponse booléenne et l'expliciter rend l'intention lisible au point d'appel.</param>
/// <param name="Tokens">Somme des tokens (entrée + sortie + création et lecture de cache) des réponses
/// assistant de la fenêtre interrogée. Borne inférieure, jamais un pourcentage.</param>
/// <param name="LastActivityAt">Instant de la réponse assistant la plus récente de la fenêtre, ou null
/// si la fenêtre est vide.</param>
public sealed record TranscriptActivity(bool HasActivity, long Tokens, DateTimeOffset? LastActivityAt);

/// <summary>
/// Journal MATÉRIALISÉ des réponses assistant récentes. PUR : aucune E/S, interrogeable N fois.
/// Sépare l'E/S (une seule passe disque, coûteuse) de la requête (bornage par fenêtre, gratuite) —
/// les deux fenêtres 5 h et hebdo ont des bornes basses DIFFÉRENTES mais doivent répondre depuis un
/// SEUL instantané disque, sinon leurs réponses peuvent être incohérentes entre elles.
/// </summary>
public sealed class TranscriptActivityLog
{
    // Entrées (instant, tokens) matérialisées par la passe disque. Triées croissant par le producteur,
    // mais aucune méthode d'ici n'en dépend : le bornage est un balayage complet, donc robuste.
    private readonly IReadOnlyList<(DateTimeOffset Ts, long Tokens)> _entries;

    /// <summary>Instant de la passe disque. Borne haute INCLUSIVE des requêtes.</summary>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// Borne basse au-delà de laquelle le journal ne sait RIEN (filtre mtime de 8 jours du parcours
    /// disque). Une requête dont le « depuis » est antérieur à cet horizon produirait un delta
    /// silencieusement SOUS-ÉVALUÉ : l'appelant doit alors refuser le delta plutôt que mentir.
    /// </summary>
    public DateTimeOffset Horizon { get; }

    public TranscriptActivityLog(DateTimeOffset now, DateTimeOffset horizon,
                                 IReadOnlyList<(DateTimeOffset Ts, long Tokens)> entries)
    {
        Now = now;
        Horizon = horizon;
        _entries = entries;
    }

    /// <summary>Le journal couvre-t-il une interrogation depuis <paramref name="since"/> ?</summary>
    public bool Covers(DateTimeOffset since) => since >= Horizon;

    /// <summary>
    /// Activité et tokens dans <c>]since ; Now]</c>. Borne basse EXCLUSIVE : un message dont le
    /// timestamp vaut exactement l'instant du relevé exact a déjà été compté par le serveur, le
    /// compter à nouveau le compterait DEUX FOIS. Borne haute inclusive : l'instant du relevé
    /// disque lui-même appartient à la fenêtre.
    /// </summary>
    public TranscriptActivity Since(DateTimeOffset since)
    {
        long tokens = 0;
        DateTimeOffset? derniere = null;

        foreach (var e in _entries)
        {
            if (e.Ts <= since || e.Ts > Now) continue;      // ]since ; Now]
            tokens += e.Tokens;
            if (derniere is null || e.Ts > derniere.Value) derniere = e.Ts;
        }

        return new TranscriptActivity(derniere is not null, tokens, derniere);
    }
}

/// <summary>
/// Source de DELTA (DEL-01/DEL-02). N'implémente VOLONTAIREMENT PAS <see cref="IUsageProvider"/> :
/// les transcripts ne produisent plus jamais d'utilisation absolue (EXA-04). Ne pas « rétablir »
/// cet héritage : c'est la garantie structurelle du milestone.
/// </summary>
public interface ITranscriptActivitySource
{
    /// <summary>Une SEULE passe disque, qui matérialise le journal interrogeable en mémoire.</summary>
    Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default);
}
