namespace Chronos.ViewModels;

/// <summary>Nature de ce que la vue doit faire après un événement souris, décidée par <see cref="AutomateGeste"/>.</summary>
public enum TypeActionGeste
{
    /// <summary>Rien à faire pour l'instant.</summary>
    Rien,

    /// <summary>Transmettre un clic à l'arbitre du cadran (<see cref="ActionGeste.ClickCount"/> = 1 ou plus).</summary>
    Clic,

    /// <summary>Le seuil de glisser est franchi : lancer le déplacement de la fenêtre.</summary>
    CommencerGlisser,
}

/// <summary>Sortie de l'automate de geste. <see cref="ClickCount"/> n'a de sens que pour <see cref="TypeActionGeste.Clic"/>.</summary>
public readonly record struct ActionGeste(TypeActionGeste Type, int ClickCount = 0)
{
    /// <summary>Aucune action.</summary>
    public static readonly ActionGeste Rien = new(TypeActionGeste.Rien);

    /// <summary>Lancer le glisser de la fenêtre.</summary>
    public static readonly ActionGeste CommencerGlisser = new(TypeActionGeste.CommencerGlisser);

    /// <summary>Un clic à transmettre à l'arbitre, avec le compte de clics de WPF.</summary>
    public static ActionGeste Clic(int clickCount) => new(TypeActionGeste.Clic, clickCount);

    /// <summary>Vrai si l'action est un clic à transmettre.</summary>
    public bool EstClic => Type == TypeActionGeste.Clic;
}

/// <summary>États de <see cref="AutomateGeste"/>.</summary>
public enum EtatGeste
{
    /// <summary>Aucun bouton enfoncé (ou geste terminé).</summary>
    Repos,

    /// <summary>Bouton enfoncé, décision clic / glisser pas encore prise.</summary>
    Appuye,

    /// <summary>Le glisser a commencé : le relâchement et la perte de capture qui suivent sont ignorés.</summary>
    Glisse,

    /// <summary>Double-clic déjà transmis à l'appui : le reste du geste est ignoré.</summary>
    Consomme,
}

/// <summary>
/// GST-01 (R7, plan de design § 2) — automate PUR du geste unique sur toute la silhouette du cadran : clic,
/// double-clic et glisser partagent la même surface. La vue lui traduit les événements souris ; il ne connaît ni WPF
/// ni l'heure (positions et seuils en DIP passés en paramètres), comme <see cref="ArbitreClicCentre"/>, qui reste
/// inchangé et reçoit les clics produits ici.
///
/// <para><b>Double-clic décidé à l'appui.</b> <c>ClickCount</c> n'est renseigné que sur l'appui (le relâchement porte 0).
/// L'arbitre arme la bascule au premier relâchement ; WPF compte le second appui double si l'écart entre les deux
/// appuis est inférieur au délai système, donc ce second appui arrive toujours AVANT l'échéance de l'arbitre : aucune
/// bascule parasite. Décider au second relâchement ouvrirait une course si ce second appui est tenu plus longtemps.</para>
///
/// <para><b>Simple clic armé au relâchement.</b> C'est le seul moment où l'on sait qu'il n'y a pas eu de glisser.
/// L'arbitre n'a pas d'« annuler » et doit rester tel quel : armer à l'appui obligerait à le modifier.</para>
///
/// <para><b>Seuil strict, par axe.</b> <c>SM_CXDRAG</c> / <c>SM_CYDRAG</c> comptent les pixels « de chaque côté » du point
/// d'appui : le glisser commence quand un seul axe dépasse STRICTEMENT son seuil.</para>
///
/// <para><b>Réentrance de <c>DragMove</c>.</b> <c>DragMove</c> envoie lui-même un relâchement synthétique pendant son appel ;
/// l'automate passe en <see cref="EtatGeste.Glisse"/> AVANT de rendre <see cref="ActionGeste.CommencerGlisser"/>, si bien
/// que ce relâchement (et la perte de capture provoquée par la boucle système) ne produisent aucun clic.</para>
///
/// <para><b>Juste après un glisser</b>, la fenêtre a suivi le curseur : WPF voit « le même endroit client » et compterait
/// double un appui rapide. Le premier appui qui suit un glisser est donc traité comme un simple appui.</para>
/// </summary>
public sealed class AutomateGeste
{
    private EtatGeste _etat = EtatGeste.Repos;
    private double _x0;
    private double _y0;
    private bool _dernierEtaitGlisser;

    /// <summary>État courant (lecture seule).</summary>
    public EtatGeste Etat => _etat;

    /// <summary>Appui du bouton gauche en (<paramref name="x"/>, <paramref name="y"/>) DIP, avec le <c>ClickCount</c> de WPF.
    /// Repart de zéro quel que soit l'état : un relâchement perdu ne bloque jamais l'automate.</summary>
    public ActionGeste Appui(double x, double y, int clickCount)
    {
        var apresGlisser = _dernierEtaitGlisser;
        _dernierEtaitGlisser = false;                // drapeau lu puis remis à zéro à chaque appui

        if (clickCount >= 2 && !apresGlisser)
        {
            _etat = EtatGeste.Consomme;              // double-clic : transmis tout de suite, le reste du geste est ignoré
            return ActionGeste.Clic(clickCount);
        }

        _x0 = x;
        _y0 = y;
        _etat = EtatGeste.Appuye;
        return ActionGeste.Rien;
    }

    /// <summary>Déplacement du pointeur en (<paramref name="x"/>, <paramref name="y"/>) DIP ; seuils en DIP
    /// (<c>SystemParameters.MinimumHorizontalDragDistance</c> / <c>MinimumVerticalDragDistance</c>, passés par la vue).</summary>
    public ActionGeste Deplacement(double x, double y, double seuilX, double seuilY)
    {
        if (_etat != EtatGeste.Appuye) return ActionGeste.Rien;

        if (Math.Abs(x - _x0) > seuilX || Math.Abs(y - _y0) > seuilY)
        {
            _etat = EtatGeste.Glisse;                // AVANT DragMove : son relâchement synthétique tombera ici
            _dernierEtaitGlisser = true;
            return ActionGeste.CommencerGlisser;
        }

        return ActionGeste.Rien;
    }

    /// <summary>Relâchement du bouton gauche. Ne lit jamais <c>ClickCount</c> (il vaut 0 au relâchement).</summary>
    public ActionGeste Relache()
    {
        var etait = _etat;
        _etat = EtatGeste.Repos;
        return etait == EtatGeste.Appuye ? ActionGeste.Clic(1) : ActionGeste.Rien;
    }

    /// <summary>Perte de la capture souris : annule un appui en cours (Alt+Tab, fenêtre système…) ; ignorée pendant un
    /// glisser, que la boucle système de <c>DragMove</c> provoque elle-même.</summary>
    public void PerteCapture()
    {
        if (_etat == EtatGeste.Appuye) _etat = EtatGeste.Repos;
    }
}
