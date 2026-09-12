namespace Chronos.Models;

/// <summary>
/// Usage en DÉPASSEMENT (overage, HDR-04) : un fait de COMPTE, et non une troisième fenêtre.
///
/// Pourquoi ce n'est pas une <c>WindowKind</c> de plus : le code d'origine lit la famille overage dans une
/// branche ALTERNATIVE (<c>elif</c>) liée au type de compte — cette forme de réponse n'a ni 5 h ni 7 j.
/// Or <c>UsageSnapshot</c> n'a que deux emplacements <c>required</c>, et aucun champ de snapshot ne
/// survit à <c>CompositeUsageProvider</c> (qui reconstruit le record au lieu de le copier, et dont
/// l'édition est interdite jusqu'à la phase 19) : une troisième fenêtre n'aurait littéralement nulle part
/// où vivre en phase 18.
///
/// Tous les champs sont optionnels : rien n'est jamais inventé, et l'absence ne se déguise pas en zéro.
/// </summary>
public sealed record EtatDepassement
{
    /// <summary>Fraction d'usage en dépassement, en 0..1 (les en-têtes sont déjà en fraction).
    /// null = non rapporté. Non plafonnée : un dépassement réel doit rester visible.</summary>
    public double? Utilization { get; init; }

    /// <summary>Instant de reset du dépassement. null = non rapporté.</summary>
    public DateTimeOffset? ResetsAt { get; init; }

    /// <summary>Statut que le serveur associe au dépassement. null = non rapporté ;
    /// <see cref="StatutServeur.NonReconnu"/> = rapporté mais illisible — deux faits distincts.</summary>
    public StatutServeur? Statut { get; init; }

    /// <summary>Vrai dès qu'UNE information a été rapportée. Sert à ne pas publier, ni afficher, un
    /// dépassement entièrement vide — qui ne dirait rien tout en ayant l'air d'un fait.</summary>
    public bool EstRenseigne => Utilization is not null || ResetsAt is not null || Statut is not null;

    /// <summary>
    /// Vrai seulement si un dépassement est RÉELLEMENT EN COURS — c'est-à-dire s'il porte une quantité
    /// ou un instant de reset. Un <see cref="Statut"/> seul ne dit PAS qu'il y a dépassement : il déclare
    /// la POLITIQUE du compte à son sujet.
    ///
    /// Constaté en production le 2026-09-12 sur un compte Max x20 à 23 % d'usage : le serveur envoie
    /// <c>anthropic-ratelimit-unified-overage-status</c> SANS aucune utilisation ni reset, et les deux
    /// fenêtres disent par ailleurs « autorisé ». Le lire comme un dépassement rapporté faisait afficher
    /// « serveur : REJETÉ » — un contresens alarmant : le serveur ne refusait rien, il déclarait que le
    /// dépassement n'est pas autorisé sur ce compte.
    ///
    /// <see cref="EstRenseigne"/> reste le garde-fou de publication (« le serveur a dit quelque chose »),
    /// celui-ci est le garde-fou d'AFFICHAGE (« il se passe quelque chose »). Les deux sont nécessaires et
    /// ne se confondent pas.
    /// </summary>
    public bool EstEnCours => Utilization is not null || ResetsAt is not null;
}
