namespace Chronos.ViewModels;

/// <summary>Ce que le cadran doit faire d'un clic au centre, décidé par <see cref="ArbitreClicCentre"/>.</summary>
public enum ActionClicCentre
{
    /// <summary>Rien pour l'instant : un simple clic est ARMÉ, sa bascule attend l'échéance du délai de double-clic.</summary>
    Rien,

    /// <summary>Le délai de double-clic est écoulé sans second clic : bascule pourcentages ↔ temps avant reset.</summary>
    Basculer,

    /// <summary>Double-clic (ou plus) : ouvrir la fenêtre Historique ; la bascule armée est ANNULÉE.</summary>
    OuvrirHistorique,
}

/// <summary>
/// ACC-02 / D-35-06 — arbitre PUR du clic au centre du cadran : TEMPORISATION, pas annulation.
///
/// <para>Annuler après coup ferait basculer puis re-basculer (% → temps → %) : deux bascules réelles et un
/// clignotement. Ici le premier clic ARME une bascule qui n'a lieu qu'à l'échéance du délai de double-clic du
/// système ; un second clic (<c>ClickCount &gt;= 2</c> — WPF applique déjà le délai ET le rectangle système)
/// désarme l'attente et ouvre l'Historique. Coût assumé : le simple clic bascule après ce délai (≈ 0,5 s).</para>
///
/// <para>Aucun type WPF, aucune horloge interne : l'instant est PASSÉ à chaque appel (horloge injectée par le
/// ViewModel), la décision est une fonction du temps — testable sans minuterie ni attente réelle.
/// Cas limite (Pitfall 7) : si l'échéance est traitée avant l'arrivée du second clic, on a une bascule puis
/// une ouverture — une bascule au plus, jamais deux.</para>
/// </summary>
public sealed class ArbitreClicCentre
{
    private DateTimeOffset? _enAttenteDepuis;

    /// <param name="delai">Délai de double-clic de l'utilisateur ; strictement positif.</param>
    public ArbitreClicCentre(TimeSpan delai)
    {
        if (delai <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delai), delai, "Le délai de double-clic doit être strictement positif.");
        Delai = delai;
    }

    /// <summary>Délai de double-clic appliqué à l'échéance.</summary>
    public TimeSpan Delai { get; }

    /// <summary>Vrai tant qu'une bascule est armée et pas encore décidée.</summary>
    public bool EnAttente => _enAttenteDepuis is not null;

    /// <summary>Un clic au centre à l'instant <paramref name="t"/>, avec le compte de clics de WPF.</summary>
    public ActionClicCentre Clic(int clickCount, DateTimeOffset t)
    {
        if (clickCount >= 2)
        {
            _enAttenteDepuis = null;                 // la bascule armée par le premier clic n'aura JAMAIS lieu
            return ActionClicCentre.OuvrirHistorique;
        }

        _enAttenteDepuis = t;                        // armée, pas encore décidée
        return ActionClicCentre.Rien;
    }

    /// <summary>L'échéance est-elle atteinte à l'instant <paramref name="t"/> ? <see cref="ActionClicCentre.Basculer"/>
    /// une seule fois par clic armé, <see cref="ActionClicCentre.Rien"/> sinon (trop tôt, rien d'armé, déjà décidé).</summary>
    public ActionClicCentre Echeance(DateTimeOffset t)
    {
        if (_enAttenteDepuis is { } t0 && t - t0 >= Delai)
        {
            _enAttenteDepuis = null;
            return ActionClicCentre.Basculer;
        }

        return ActionClicCentre.Rien;
    }

    /// <summary>Temps qu'il reste avant l'échéance à l'instant <paramref name="t"/> (<see cref="TimeSpan.Zero"/> si rien
    /// n'est armé ou si l'échéance est passée). Sert à RÉARMER la minuterie quand elle se déclenche un peu tôt : une
    /// minuterie à la granularité du système ne doit jamais faire perdre un simple clic.</summary>
    public TimeSpan Restant(DateTimeOffset t)
    {
        if (_enAttenteDepuis is not { } t0) return TimeSpan.Zero;
        var restant = Delai - (t - t0);
        return restant > TimeSpan.Zero ? restant : TimeSpan.Zero;
    }
}
