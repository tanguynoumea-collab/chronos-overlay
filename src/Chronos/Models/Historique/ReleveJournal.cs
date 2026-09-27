namespace Chronos.Models.Historique;

/// <summary>
/// JRN-01 — un relevé EXACT d'UNE source à UN instant, tel que le journal l'écrit et le relit (D-32-15).
///
/// <para>Une ligne = un couple (<c>t</c>, <c>source</c>) : c'est la clé d'idempotence qui permet à deux
/// processus d'écrire le même relevé sans le doubler. Si le composite mélange (5 h de la sonde, hebdo
/// d'un repli), chaque couple distinct fait SA ligne, les fenêtres de l'autre couple restant à
/// <c>null</c> — un <c>null</c> est une absence, jamais un zéro.</para>
///
/// <para><see cref="U5"/> et <see cref="U7"/> sont des fractions 0..1 BRUTES, écrites sans arrondi
/// (D-32-18) : la granularité 0,01 constatée sur les en-têtes est une hypothèse qui se vérifie AVEC le
/// journal, pas une règle qu'on lui impose avant. <see cref="T"/>, <see cref="R5"/> et <see cref="R7"/>
/// sont des instants UTC (ISO 8601 sur le fil).</para>
///
/// <para>Type NEUTRE (aucun WPF), sous garde de <c>ServicesLayerPurityTests</c> élargie aux sous-namespaces.</para>
/// </summary>
/// <param name="T">Instant de capture (<c>WindowState.CapturedAt</c>), UTC.</param>
/// <param name="Source">Qui a produit le chiffre. Jamais <c>MagasinDernierExact</c> : le journal n'écrit que l'inner brut.</param>
/// <param name="U5">Utilisation 5 h, fraction 0..1 brute ; <c>null</c> si la fenêtre 5 h n'est pas de ce couple.</param>
/// <param name="R5">Reset 5 h annoncé par le serveur, UTC ; <c>null</c> si non rapporté.</param>
/// <param name="Statut5">Statut serveur de la fenêtre 5 h ; <c>null</c> si non rapporté.</param>
/// <param name="U7">Utilisation hebdo, fraction 0..1 brute ; <c>null</c> si la fenêtre hebdo n'est pas de ce couple.</param>
/// <param name="R7">Reset hebdo annoncé par le serveur, UTC ; <c>null</c> si non rapporté.</param>
/// <param name="Statut7">Statut serveur de la fenêtre hebdo ; <c>null</c> si non rapporté.</param>
/// <param name="Overage">Fraction d'usage en dépassement (HDR-04) ; <c>null</c> si aucun dépassement rapporté.</param>
/// <param name="OverageStatut">Statut que le serveur associe au dépassement (peut exister SEUL : politique du compte, 2026-09-12).</param>
public sealed record ReleveJournal(
    DateTimeOffset T,
    SourceUsage Source,
    double? U5,
    DateTimeOffset? R5,
    StatutServeur? Statut5,
    double? U7,
    DateTimeOffset? R7,
    StatutServeur? Statut7,
    double? Overage,
    StatutServeur? OverageStatut);
